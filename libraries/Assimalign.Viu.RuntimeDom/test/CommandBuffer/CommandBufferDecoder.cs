using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu.RuntimeDom.Tests;

// The C# reference decoder for the interop command buffer ([V01.01.04.05]): it decodes a finalized
// frame and replays every op onto an InMemoryHandleDom, collecting the handles that remove /
// setElementText released and returning them as the single apply-call result (the same shape the JS
// applier returns and the same shape PurgeReleasedHandles consumes). It mirrors viu-dom.js's
// applyCommandBuffer op-for-op; the browser applier is cross-checked live by the reviewer, this
// decoder is the DOM-free CI oracle. Validates magic/version so a drifted frame fails loudly.
internal static class CommandBufferDecoder
{
    internal static int[] Apply(byte[] frame, int length, InMemoryHandleDom dom)
    {
        if (length < DomCommandBuffer.HeaderSize)
        {
            throw new InvalidOperationException("Command buffer frame is shorter than its header.");
        }
        if (frame[0] != DomCommandBuffer.Magic || frame[1] != DomCommandBuffer.Version)
        {
            throw new InvalidOperationException(
                $"Command buffer version mismatch: expected {DomCommandBuffer.Magic:X2}/{DomCommandBuffer.Version:X2}, "
                + $"got {frame[0]:X2}/{frame[1]:X2}.");
        }
        var span = frame.AsSpan(0, length);
        var operationCount = BinaryPrimitives.ReadInt32LittleEndian(span[2..]);
        var stringTableOffset = BinaryPrimitives.ReadInt32LittleEndian(span[10..]);
        var strings = ReadStringTable(span, stringTableOffset);

        var released = new List<int>();
        var cursor = DomCommandBuffer.HeaderSize;
        for (var index = 0; index < operationCount; index++)
        {
            var opcode = (DomCommandOpcode)span[cursor++];
            switch (opcode)
            {
                case DomCommandOpcode.CreateElement:
                    dom.CreateElementAs(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!, Str(strings, ReadInt(span, ref cursor)));
                    break;
                case DomCommandOpcode.CreateText:
                    dom.CreateTextAs(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.CreateComment:
                    dom.CreateCommentAs(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.SetText:
                    dom.SetText(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.SetElementText:
                    released.AddRange(dom.SetElementText(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!));
                    break;
                case DomCommandOpcode.Insert:
                    dom.Insert(ReadInt(span, ref cursor), ReadInt(span, ref cursor), ReadInt(span, ref cursor));
                    break;
                case DomCommandOpcode.Remove:
                    released.AddRange(dom.Remove(ReadInt(span, ref cursor)));
                    break;
                case DomCommandOpcode.SetAttribute:
                    dom.SetAttribute(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!, Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.RemoveAttribute:
                    dom.RemoveAttribute(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.SetXlinkAttribute:
                    dom.SetXlinkAttribute(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!, Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.RemoveXlinkAttribute:
                    dom.RemoveXlinkAttribute(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.SetClassName:
                    dom.SetClassName(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.SetStringProperty:
                    dom.SetStringProperty(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!, Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.SetBooleanProperty:
                    dom.SetBooleanProperty(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!, ReadBool(span, ref cursor));
                    break;
                case DomCommandOpcode.SetValueGuarded:
                    dom.SetValueGuarded(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.SetStyleText:
                    dom.SetStyleText(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.SetStyleProperty:
                    dom.SetStyleProperty(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!, Str(strings, ReadInt(span, ref cursor))!, ReadBool(span, ref cursor));
                    break;
                case DomCommandOpcode.RemoveStyleProperty:
                    dom.RemoveStyleProperty(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.AddEventListener:
                    dom.AddEventListener(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!, ReadBool(span, ref cursor), ReadBool(span, ref cursor), ReadBool(span, ref cursor));
                    break;
                case DomCommandOpcode.RemoveEventListener:
                    dom.RemoveEventListener(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!, ReadBool(span, ref cursor));
                    break;
                case DomCommandOpcode.AddTransitionClass:
                    dom.AddTransitionClass(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.RemoveTransitionClass:
                    dom.RemoveTransitionClass(ReadInt(span, ref cursor), Str(strings, ReadInt(span, ref cursor))!);
                    break;
                case DomCommandOpcode.ForceReflow:
                    // The reflow barrier carries no operands; the real applier reads document.body.offsetHeight.
                    dom.ForceReflow();
                    break;
                case DomCommandOpcode.SetMoveTransform:
                    // The first float64 operands in the wire format: the FLIP inverting delta ([V01.01.04.07.03]).
                    dom.SetMoveTransform(ReadInt(span, ref cursor), ReadDouble(span, ref cursor), ReadDouble(span, ref cursor));
                    break;
                case DomCommandOpcode.ClearMoveStyles:
                    dom.ClearMoveStyles(ReadInt(span, ref cursor));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown command-buffer opcode {(byte)opcode}.");
            }
        }
        return [.. released];
    }

    private static string?[] ReadStringTable(ReadOnlySpan<byte> span, int offset)
    {
        var count = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]);
        offset += 4;
        var strings = new string?[count];
        for (var index = 0; index < count; index++)
        {
            var byteLength = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]);
            offset += 4;
            strings[index] = Encoding.UTF8.GetString(span.Slice(offset, byteLength));
            offset += byteLength;
        }
        return strings;
    }

    private static string? Str(string?[] strings, int index) => index < 0 ? null : strings[index];

    private static int ReadInt(ReadOnlySpan<byte> span, ref int cursor)
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(span[cursor..]);
        cursor += 4;
        return value;
    }

    private static bool ReadBool(ReadOnlySpan<byte> span, ref int cursor) => span[cursor++] != 0;

    private static double ReadDouble(ReadOnlySpan<byte> span, ref int cursor)
    {
        var value = BinaryPrimitives.ReadDoubleLittleEndian(span[cursor..]);
        cursor += 8;
        return value;
    }
}
