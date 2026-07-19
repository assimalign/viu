using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu.RuntimeDom;

/// <summary>
/// The .NET writer half of the interop command buffer ([V01.01.04.05]): buffered node-ops encode
/// each DOM mutation as an opcode + operands into one growable, reused <see cref="byte"/> array, and
/// a single interop call hands the whole frame to the JS applier (<c>viu-dom.js</c>'s
/// <c>applyCommandBuffer</c>) per scheduler flush — collapsing hundreds of boundary crossings into
/// one. There is no upstream Vue counterpart; the prior art is Blazor's <c>RenderBatch</c>. The
/// design is behaviorally invisible: buffered and direct modes produce identical DOM.
/// <para>
/// <b>Handles are pre-allocated on the .NET side.</b> A buffered <c>createElement/Text/Comment</c>
/// cannot return the JS handle (the call is one-way), so <see cref="AllocateHandle"/> draws the id
/// from this counter and the op carries "create X AS handle N"; the JS applier registers the new
/// node under N into the same registry the direct path uses. The counter survives across flushes and
/// is carried in every frame header so the JS side keeps its own foreign-node allocator ahead of it.
/// </para>
/// <para>
/// <b>Frame layout</b> (all multi-byte integers little-endian, explicit via
/// <see cref="BinaryPrimitives"/>; the JS applier reads a <c>DataView</c> with <c>littleEndian=true</c>):
/// <code>
/// [0]        magic  (byte)  = 0xB6
/// [1]        version(byte)  = 0x01
/// [2..6)     opCount           (int32)
/// [6..10)    nextHandle        (int32)  -- the .NET counter after this batch; JS adopts max(its, this)
/// [10..14)   stringTableOffset (int32)  -- absolute byte offset where the string table begins
/// [14..)     ops: each = [opcode:byte] then operands (int32 handles/indices, 1-byte bools)
/// [offset]   stringCount(int32), then per string: [utf8ByteLength:int32][utf8 bytes]
/// </code>
/// Strings (tags, prop/event names, values) are interned per flush and referenced by int32 index; a
/// null string is index <c>-1</c>. The <c>magic</c>/<c>version</c> pair lets the applier reject a
/// drifted or corrupt frame loudly instead of misapplying it.
/// </para>
/// Zero per-flush managed allocation at steady state: the byte array, the intern dictionary, and the
/// string list are cleared and reused; growth (array doubling) happens only when a flush exceeds the
/// current capacity. Not thread-safe (single-threaded JS event-loop model).
/// </summary>
internal sealed class DomCommandBuffer
{
    /// <summary>The frame's leading byte — a sanity marker the applier validates.</summary>
    internal const byte Magic = 0xB6;

    /// <summary>The frame format version — bump on any layout or opcode change so drift fails loudly.</summary>
    internal const byte Version = 0x01;

    /// <summary>Header size: magic + version + opCount + nextHandle + stringTableOffset.</summary>
    internal const int HeaderSize = 2 + 4 + 4 + 4;

    /// <summary>Index sentinel for a null string operand.</summary>
    internal const int NullStringIndex = -1;

    private readonly Dictionary<string, int> _stringIndex = new(StringComparer.Ordinal);
    private readonly List<string> _strings = [];
    private byte[] _buffer;
    private int _position;
    private int _operationCount;
    private int _nextHandle = 1; // JS handle registry starts at 1 (0 is the "no node" sentinel).

    /// <summary>Creates a buffer with an initial capacity in bytes (grows as needed).</summary>
    /// <param name="initialCapacity">The starting byte-array size.</param>
    internal DomCommandBuffer(int initialCapacity = 8192)
    {
        _buffer = new byte[Math.Max(HeaderSize, initialCapacity)];
        ResetFrame();
    }

    /// <summary>Whether any op has been written since the last <see cref="ResetFrame"/>.</summary>
    internal bool HasPendingOperations => _operationCount > 0;

    /// <summary>The number of ops written to the current frame (diagnostics/tests).</summary>
    internal int OperationCount => _operationCount;

    /// <summary>The next handle this buffer would allocate (tests/diagnostics).</summary>
    internal int NextHandle => _nextHandle;

    /// <summary>The backing array; valid only up to the length <see cref="FinalizeFrame"/> returns.</summary>
    internal byte[] BackingArray => _buffer;

    /// <summary>
    /// Reserves and returns the next node handle from the .NET-side counter (upstream: the JS
    /// <c>registerNode</c> id, moved to .NET so a one-way create op can name its result).
    /// </summary>
    internal int AllocateHandle() => _nextHandle++;

    /// <summary>
    /// Raises the handle counter above a handle the JS side just allocated for a foreign node (a
    /// <c>parentNode</c>/<c>nextSibling</c>/<c>querySelector</c> result), keeping the two allocators
    /// from ever issuing the same id. Called after a forced-flush read returns.
    /// </summary>
    /// <param name="foreignHandle">The handle the bridge returned.</param>
    internal void ObserveForeignHandle(int foreignHandle)
    {
        if (foreignHandle >= _nextHandle)
        {
            _nextHandle = foreignHandle + 1;
        }
    }

    /// <summary>Clears the frame for reuse — keeps the handle counter, which spans flushes.</summary>
    internal void ResetFrame()
    {
        _position = HeaderSize;
        _operationCount = 0;
        _stringIndex.Clear();
        _strings.Clear();
    }

    /// <summary>
    /// Appends the string table, back-patches the header, and returns the frame's total byte length.
    /// Read <see cref="BackingArray"/> only after this call — the string-table write may have grown
    /// (reallocated) the array.
    /// </summary>
    internal int FinalizeFrame()
    {
        var stringTableOffset = _position;
        EnsureCapacity(4);
        WriteInt32Raw(_strings.Count);
        foreach (var value in _strings)
        {
            var byteCount = Encoding.UTF8.GetByteCount(value);
            EnsureCapacity(4 + byteCount);
            WriteInt32Raw(byteCount);
            Encoding.UTF8.GetBytes(value, _buffer.AsSpan(_position, byteCount));
            _position += byteCount;
        }
        _buffer[0] = Magic;
        _buffer[1] = Version;
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(2), _operationCount);
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(6), _nextHandle);
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(10), stringTableOffset);
        return _position;
    }

    // --- op writers (one per buffered bridge op; operand order mirrors the JS applier) ------------

    internal void WriteCreateElement(int handle, string tag, string? elementNamespace)
    {
        WriteOpcode(DomCommandOpcode.CreateElement);
        WriteInt32(handle);
        WriteStringReference(tag);
        WriteStringReference(elementNamespace);
    }

    internal void WriteCreateText(int handle, string text)
    {
        WriteOpcode(DomCommandOpcode.CreateText);
        WriteInt32(handle);
        WriteStringReference(text);
    }

    internal void WriteCreateComment(int handle, string text)
    {
        WriteOpcode(DomCommandOpcode.CreateComment);
        WriteInt32(handle);
        WriteStringReference(text);
    }

    internal void WriteSetText(int handle, string text)
    {
        WriteOpcode(DomCommandOpcode.SetText);
        WriteInt32(handle);
        WriteStringReference(text);
    }

    internal void WriteSetElementText(int handle, string text)
    {
        WriteOpcode(DomCommandOpcode.SetElementText);
        WriteInt32(handle);
        WriteStringReference(text);
    }

    internal void WriteInsert(int parentHandle, int childHandle, int anchorHandle)
    {
        WriteOpcode(DomCommandOpcode.Insert);
        WriteInt32(parentHandle);
        WriteInt32(childHandle);
        WriteInt32(anchorHandle);
    }

    internal void WriteRemove(int childHandle)
    {
        WriteOpcode(DomCommandOpcode.Remove);
        WriteInt32(childHandle);
    }

    internal void WriteSetAttribute(int handle, string name, string value)
        => WriteHandleNameValue(DomCommandOpcode.SetAttribute, handle, name, value);

    internal void WriteRemoveAttribute(int handle, string name)
        => WriteHandleName(DomCommandOpcode.RemoveAttribute, handle, name);

    internal void WriteSetXlinkAttribute(int handle, string name, string value)
        => WriteHandleNameValue(DomCommandOpcode.SetXlinkAttribute, handle, name, value);

    internal void WriteRemoveXlinkAttribute(int handle, string name)
        => WriteHandleName(DomCommandOpcode.RemoveXlinkAttribute, handle, name);

    internal void WriteSetClassName(int handle, string value)
    {
        WriteOpcode(DomCommandOpcode.SetClassName);
        WriteInt32(handle);
        WriteStringReference(value);
    }

    internal void WriteSetStringProperty(int handle, string name, string value)
        => WriteHandleNameValue(DomCommandOpcode.SetStringProperty, handle, name, value);

    internal void WriteSetBooleanProperty(int handle, string name, bool value)
    {
        WriteOpcode(DomCommandOpcode.SetBooleanProperty);
        WriteInt32(handle);
        WriteStringReference(name);
        WriteBoolean(value);
    }

    internal void WriteSetValueGuarded(int handle, string value)
    {
        WriteOpcode(DomCommandOpcode.SetValueGuarded);
        WriteInt32(handle);
        WriteStringReference(value);
    }

    internal void WriteSetStyleText(int handle, string cssText)
    {
        WriteOpcode(DomCommandOpcode.SetStyleText);
        WriteInt32(handle);
        WriteStringReference(cssText);
    }

    internal void WriteSetStyleProperty(int handle, string name, string value, bool important)
    {
        WriteOpcode(DomCommandOpcode.SetStyleProperty);
        WriteInt32(handle);
        WriteStringReference(name);
        WriteStringReference(value);
        WriteBoolean(important);
    }

    internal void WriteRemoveStyleProperty(int handle, string name)
        => WriteHandleName(DomCommandOpcode.RemoveStyleProperty, handle, name);

    internal void WriteAddEventListener(int handle, string eventName, bool once, bool capture, bool passive)
    {
        WriteOpcode(DomCommandOpcode.AddEventListener);
        WriteInt32(handle);
        WriteStringReference(eventName);
        WriteBoolean(once);
        WriteBoolean(capture);
        WriteBoolean(passive);
    }

    internal void WriteRemoveEventListener(int handle, string eventName, bool capture)
    {
        WriteOpcode(DomCommandOpcode.RemoveEventListener);
        WriteInt32(handle);
        WriteStringReference(eventName);
        WriteBoolean(capture);
    }

    // --- primitives ------------------------------------------------------------------------------

    private void WriteHandleName(DomCommandOpcode opcode, int handle, string name)
    {
        WriteOpcode(opcode);
        WriteInt32(handle);
        WriteStringReference(name);
    }

    private void WriteHandleNameValue(DomCommandOpcode opcode, int handle, string name, string value)
    {
        WriteOpcode(opcode);
        WriteInt32(handle);
        WriteStringReference(name);
        WriteStringReference(value);
    }

    private void WriteOpcode(DomCommandOpcode opcode)
    {
        EnsureCapacity(1);
        _buffer[_position++] = (byte)opcode;
        _operationCount++;
    }

    private void WriteInt32(int value)
    {
        EnsureCapacity(4);
        WriteInt32Raw(value);
    }

    private void WriteInt32Raw(int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_position, 4), value);
        _position += 4;
    }

    private void WriteBoolean(bool value)
    {
        EnsureCapacity(1);
        _buffer[_position++] = value ? (byte)1 : (byte)0;
    }

    private void WriteStringReference(string? value)
    {
        if (value is null)
        {
            WriteInt32(NullStringIndex);
            return;
        }
        if (!_stringIndex.TryGetValue(value, out var index))
        {
            index = _strings.Count;
            _strings.Add(value);
            _stringIndex[value] = index;
        }
        WriteInt32(index);
    }

    private void EnsureCapacity(int additional)
    {
        var required = _position + additional;
        if (required <= _buffer.Length)
        {
            return;
        }
        var capacity = _buffer.Length * 2;
        while (capacity < required)
        {
            capacity *= 2;
        }
        Array.Resize(ref _buffer, capacity);
    }
}
