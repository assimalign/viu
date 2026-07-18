using System;
using System.Runtime.Versioning;

using Assimalign.Vue.RuntimeCore;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The buffered <see cref="RendererOptions{TNode}"/> ([V01.01.04.05]): the same adapter shape the
/// renderer and RuntimeCore see in direct mode (<see cref="BrowserNodeOperations"/>), but every
/// write op — create/insert/remove/text and every <c>patchProp</c> leaf — encodes into a
/// <see cref="DomCommandBuffer"/> instead of crossing the interop boundary, and the whole frame
/// applies in one call at the scheduler flush boundary (<see cref="Scheduler.FlushBoundaryCallback"/>).
/// Buffered and direct modes produce byte-identical DOM; the choice is a construction-time toggle on
/// <see cref="BrowserRuntime"/>.
/// <para>
/// <b>Reads force a flush.</b> Ops that genuinely return data — <c>parentNode</c>,
/// <c>nextSibling</c>, <c>querySelector</c>, <c>insertStaticContent</c> — cannot be answered from the
/// unapplied buffer, so they commit the pending batch first (one apply call) and then read the live
/// bridge, folding the returned handle into the .NET counter so the two handle allocators never
/// collide. Handles for created nodes are pre-allocated from <see cref="DomCommandBuffer.AllocateHandle"/>
/// (the create op is one-way and cannot return the JS id).
/// </para>
/// <para>
/// <b>Released handles</b> from <c>remove</c>/<c>setElementText</c> are not returned per op; the JS
/// applier collects every handle it releases while draining the batch and returns them from the single
/// apply call, which purges the invoker registry once — the same
/// <see cref="BrowserEventInvokerRegistry.PurgeReleasedHandles"/> path the direct mode drives per op.
/// </para>
/// Ambient by activation: <see cref="Activate"/> points the flush seam, the event dispatcher, and the
/// directive operations at this instance; <see cref="Deactivate"/> restores them. Single active
/// buffered renderer per process (single-threaded JS event-loop model) — NOT thread-safe.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class BufferedBrowserNodeOperations
{
    private readonly DomCommandBuffer _buffer = new();
    private readonly BrowserEventInvokerRegistry _invokers;
    private readonly BrowserPropertyLeafOperations _leaf;
    private readonly Func<byte[], int, int[]> _applier;
    private readonly Func<string, int> _querySelector;
    private readonly Func<int, int> _parentNode;
    private readonly Func<int, int> _nextSibling;
    private readonly Func<string, int, int, string?, (int First, int Last)> _insertStaticContent;

    private Action? _previousFlushBoundary;
    private Func<int, bool, BrowserEvent, int>? _previousDispatcher;
    private BrowserDirectiveOperations? _previousDirectiveOperations;
    private bool _isActive;
    private int _interopCallCount;

    internal BufferedBrowserNodeOperations(
        Func<byte[], int, int[]> applier,
        Func<string, int> querySelector,
        Func<int, int> parentNode,
        Func<int, int> nextSibling,
        Func<string, int, int, string?, (int First, int Last)> insertStaticContent)
    {
        _applier = applier;
        _querySelector = querySelector;
        _parentNode = parentNode;
        _nextSibling = nextSibling;
        _insertStaticContent = insertStaticContent;
        _invokers = new BrowserEventInvokerRegistry(
            (handle, eventName, once, capture, passive) => _buffer.WriteAddEventListener(handle, eventName, once, capture, passive),
            (handle, eventName, capture) => _buffer.WriteRemoveEventListener(handle, eventName, capture));
        _leaf = new BrowserPropertyLeafOperations
        {
            SetAttribute = (element, name, value) => _buffer.WriteSetAttribute(element, name, value),
            RemoveAttribute = (element, name) => _buffer.WriteRemoveAttribute(element, name),
            SetXlinkAttribute = (element, name, value) => _buffer.WriteSetXlinkAttribute(element, name, value),
            RemoveXlinkAttribute = (element, name) => _buffer.WriteRemoveXlinkAttribute(element, name),
            SetClassName = (element, value) => _buffer.WriteSetClassName(element, value),
            SetStringProperty = (element, name, value) => _buffer.WriteSetStringProperty(element, name, value),
            SetBooleanProperty = (element, name, value) => _buffer.WriteSetBooleanProperty(element, name, value),
            SetValueGuarded = (element, value) => _buffer.WriteSetValueGuarded(element, value),
            SetStyleText = (element, cssText) => _buffer.WriteSetStyleText(element, cssText),
            SetStyleProperty = (element, name, value, important) => _buffer.WriteSetStyleProperty(element, name, value, important),
            RemoveStyleProperty = (element, name) => _buffer.WriteRemoveStyleProperty(element, name),
            SetEventListener = (element, rawPropertyName, listener) => _invokers.SetListener(element, rawPropertyName, listener),
        };
    }

    /// <summary>The number of apply (boundary-crossing) calls made — the interop-call counter AC.</summary>
    internal int InteropCallCount => _interopCallCount;

    /// <summary>The invoker registry buffered listener ops register into (event dispatch target).</summary>
    internal BrowserEventInvokerRegistry Invokers => _invokers;

    /// <summary>The command buffer (diagnostics/tests).</summary>
    internal DomCommandBuffer Buffer => _buffer;

    /// <summary>Builds the buffered <see cref="RendererOptions{TNode}"/> for the renderer.</summary>
    internal RendererOptions<int> Create() => new()
    {
        Insert = (child, parent, anchor) => _buffer.WriteInsert(parent, child, anchor),
        Remove = child => _buffer.WriteRemove(child),
        CreateElement = (tag, elementNamespace) =>
        {
            var handle = _buffer.AllocateHandle();
            _buffer.WriteCreateElement(handle, tag, elementNamespace);
            return handle;
        },
        CreateText = text =>
        {
            var handle = _buffer.AllocateHandle();
            _buffer.WriteCreateText(handle, text);
            return handle;
        },
        CreateComment = text =>
        {
            var handle = _buffer.AllocateHandle();
            _buffer.WriteCreateComment(handle, text);
            return handle;
        },
        SetText = (node, text) => _buffer.WriteSetText(node, text),
        SetElementText = (node, text) => _buffer.WriteSetElementText(node, text),
        ParentNode = node => Read(_parentNode, node),
        NextSibling = node => Read(_nextSibling, node),
        PatchProperty = (element, elementTag, propertyName, previousValue, nextValue, elementNamespace) =>
            BrowserPropertyPatcher.Patch(_leaf, element, elementTag, propertyName, previousValue, nextValue, elementNamespace),
        QuerySelector = selector =>
        {
            FlushPending();
            var handle = _querySelector(selector);
            _buffer.ObserveForeignHandle(handle);
            return handle;
        },
        InsertStaticContent = (content, parent, anchor, elementNamespace) =>
        {
            FlushPending();
            var (first, last) = _insertStaticContent(content, parent, anchor, elementNamespace);
            _buffer.ObserveForeignHandle(first);
            _buffer.ObserveForeignHandle(last);
            return (first, last);
        },
    };

    /// <summary>
    /// Commits the pending batch: finalize the frame, hand it across the boundary in one call, purge
    /// the invoker delegates the applier reports released, and reset the frame for the next flush.
    /// A no-op when nothing is buffered. Armed on <see cref="Scheduler.FlushBoundaryCallback"/>.
    /// </summary>
    internal void ApplyPending()
    {
        if (!_buffer.HasPendingOperations)
        {
            return;
        }
        var length = _buffer.FinalizeFrame();
        _interopCallCount++;
        var released = _applier(_buffer.BackingArray, length);
        _buffer.ResetFrame();
        _invokers.PurgeReleasedHandles(released);
    }

    /// <summary>
    /// Folds a handle the bridge issued for a node outside the buffer (a mount container, or a
    /// <c>parentNode</c>/<c>nextSibling</c> result) into the .NET handle counter so a later buffered
    /// create never reuses it. The buffered app observes its mount container here before rendering.
    /// </summary>
    /// <param name="handle">The externally issued handle.</param>
    internal void ObserveForeignHandle(int handle) => _buffer.ObserveForeignHandle(handle);

    /// <summary>Points the flush seam, event dispatch, and directive ops at this instance.</summary>
    internal void Activate()
    {
        if (_isActive)
        {
            return;
        }
        _isActive = true;
        _previousFlushBoundary = Scheduler.FlushBoundaryCallback;
        _previousDispatcher = BrowserNodeOperations.OverrideDispatcher;
        _previousDirectiveOperations = BrowserDirectiveOperations.Current;
        Scheduler.FlushBoundaryCallback = ApplyPending;
        BrowserNodeOperations.OverrideDispatcher = _invokers.Dispatch;
        // v-model / v-show write through the same buffered leaf/invoker channels as the patch engine.
        BrowserDirectiveOperations.Current = new BrowserDirectiveOperations
        {
            SetModelListener = (element, rawPropertyName, handler) => _invokers.SetModelListener(element, rawPropertyName, handler),
            SetValueGuarded = _leaf.SetValueGuarded,
            SetBooleanProperty = _leaf.SetBooleanProperty,
            SetStyleProperty = _leaf.SetStyleProperty,
            RemoveStyleProperty = _leaf.RemoveStyleProperty,
        };
    }

    /// <summary>Restores the seam, dispatcher, and directive ops to their pre-<see cref="Activate"/> values.</summary>
    internal void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;
        Scheduler.FlushBoundaryCallback = _previousFlushBoundary;
        BrowserNodeOperations.OverrideDispatcher = _previousDispatcher;
        BrowserDirectiveOperations.Current = _previousDirectiveOperations;
    }

    /// <summary>
    /// Creates the production instance over the real bridge and activates it. Reads force a flush and
    /// then hit the live bridge; the applier is the single MemoryView interop call.
    /// </summary>
    internal static BufferedBrowserNodeOperations CreateProduction()
    {
        var operations = new BufferedBrowserNodeOperations(
            static (frame, length) => BrowserDomBridge.ApplyCommandBuffer(frame, length),
            static selector => BrowserDomBridge.QuerySelector(selector),
            static node => BrowserDomBridge.ParentNode(node),
            static node => BrowserDomBridge.NextSibling(node),
            static (content, parent, anchor, elementNamespace) =>
            {
                var span = BrowserDomBridge.InsertStaticContent(content, parent, anchor, elementNamespace);
                return (span[0], span[1]);
            });
        operations.Activate();
        return operations;
    }

    private int Read(Func<int, int> read, int node)
    {
        FlushPending();
        var handle = read(node);
        _buffer.ObserveForeignHandle(handle);
        return handle;
    }

    private void FlushPending()
    {
        if (_buffer.HasPendingOperations)
        {
            ApplyPending();
        }
    }
}
