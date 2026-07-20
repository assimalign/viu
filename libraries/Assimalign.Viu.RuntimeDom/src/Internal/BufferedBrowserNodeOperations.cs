using System;
using System.Runtime.Versioning;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.RuntimeDom;

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
/// <para>
/// <b>Transition class sequencing</b> ([V01.01.04.07.02]). CSS transitions are browser-observable
/// <em>sequencing</em>: add <c>*-enter-from</c>/<c>*-enter-active</c>, force a reflow, then swap
/// <c>*-from</c> → <c>*-to</c> on the next frame — so the class writes cannot coalesce into one style
/// recalc or the browser never fires the transition (upstream <c>Transition.ts</c> <c>forceReflow</c> +
/// double-<c>requestAnimationFrame</c> <c>nextFrame</c>). <see cref="Activate"/> therefore installs a
/// buffered <see cref="DomTransitionOperations"/> that preserves the ordering across two barrier kinds
/// rather than regressing batching to synchronous per-op flushes:
/// <list type="bullet">
/// <item><b>The reflow barrier</b> is a first-class command-buffer op
/// (<see cref="DomCommandOpcode.ForceReflow"/>, written by <see cref="DomCommandBuffer.WriteForceReflow"/>):
/// class writes stay buffered and ordered with the node ops, and the applier performs a real reflow at
/// the barrier's position while draining the <em>single</em> frame — so the frame still crosses the
/// boundary exactly once and batching is untouched.</item>
/// <item><b>The frame boundary</b> is <c>NextFrame</c>: its continuation (the <c>*-to</c> swap) is
/// scheduled through the real double-<c>requestAnimationFrame</c> and its buffered writes are applied
/// when it runs, two frames after the from/active frame committed — so the two land in distinct browser
/// frames and can never coalesce.</item>
/// </list>
/// Transition reads and listener registrations (<c>NextFrame</c>'s scheduling, <c>WhenTransitionEnds</c>,
/// <c>MeasurePosition</c>, <c>HasCssTransform</c>, the FLIP ops) force a flush and then hit the live
/// bridge, so <c>getComputedStyle</c>/<c>getBoundingClientRect</c> observe the committed classes/layout;
/// their resolve callbacks flush the finishing class removals. (FLIP <em>move</em> batching — reading all
/// positions in one pass — is the separate #163.)
/// </para>
/// Ambient by activation: <see cref="Activate"/> points the flush seam, the event dispatcher, the
/// directive operations, and the transition operations at this instance; <see cref="Deactivate"/>
/// restores them. Single active buffered renderer per process (single-threaded JS event-loop model) —
/// NOT thread-safe.
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
    private DomTransitionOperations? _previousTransitionOperations;
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

    /// <summary>Points the flush seam, event dispatch, directive ops, and transition ops at this instance.</summary>
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
        _previousTransitionOperations = DomTransitionOperations.Current;
        Scheduler.FlushBoundaryCallback = ApplyPending;
        BrowserNodeOperations.OverrideDispatcher = _invokers.Dispatch;
        // <Transition>/<TransitionGroup> class choreography writes through the buffered channel so the
        // enter/leave class sequence stays ordered with the node ops and honors the reflow + next-frame
        // barriers ([V01.01.04.07.02]). Reads/rAF/listener ops delegate to the direct (bridge-backed)
        // operations captured above, forcing a flush first so they observe committed classes/layout.
        DomTransitionOperations.Current = BuildBufferedTransitionOperations(
            _previousTransitionOperations ?? throw new InvalidOperationException(
                "No DomTransitionOperations installed to delegate reads/timing to. BrowserRuntime installs "
                + "the browser-backed transition operations before a buffered app renders; a test must install "
                + "one (recording) before Activate."));
        // v-model / v-show write through the same buffered leaf/invoker channels as the patch engine.
        BrowserDirectiveOperations.Current = new BrowserDirectiveOperations
        {
            SetModelListener = (element, rawPropertyName, handler) => _invokers.SetModelListener(element, rawPropertyName, handler),
            SetValueGuarded = _leaf.SetValueGuarded,
            SetBooleanProperty = _leaf.SetBooleanProperty,
            SetStyleProperty = _leaf.SetStyleProperty,
            RemoveStyleProperty = _leaf.RemoveStyleProperty,
            // UseCssVars in buffered mode ([V01.01.06.06]): the per-property writes join the pending frame,
            // which still commits in one boundary crossing per flush, so the batching AC holds.
            SetCssVariables = (element, names, values) =>
            {
                for (var index = 0; index < names.Length; index++)
                {
                    _leaf.SetStyleProperty(element, names[index], values[index], false);
                }
            },
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
        DomTransitionOperations.Current = _previousTransitionOperations;
    }

    // Wraps the direct (bridge-backed) transition operations for buffered mode. Class writes and the
    // reflow barrier encode into the command buffer (ordered with the node ops, applied in one frame);
    // rAF scheduling, reads, and listener registrations delegate to <paramref name="direct"/> but force
    // the pending frame to commit first so they observe the classes/layout already written, and their
    // continuations/resolves flush the writes they produce. See the class remarks ([V01.01.04.07.02]).
    private DomTransitionOperations BuildBufferedTransitionOperations(DomTransitionOperations direct) => new()
    {
        AddTransitionClass = (element, cssClass) => _buffer.WriteAddTransitionClass(element, cssClass),
        RemoveTransitionClass = (element, cssClass) => _buffer.WriteRemoveTransitionClass(element, cssClass),
        ForceReflow = () => _buffer.WriteForceReflow(),
        // The double-rAF stays real; the continuation's buffered *-to swap commits when it runs — two
        // frames after the from/active frame — so the two class states land in distinct browser frames.
        NextFrame = callback => direct.NextFrame(() =>
        {
            callback();
            ApplyPending();
        }),
        // getComputedStyle must read the *-to class the continuation just wrote: flush first, then the
        // resolve's finishing class removals (finishEnter/finishLeave) commit when the end event fires.
        WhenTransitionEnds = (element, expectedType, explicitTimeout, resolve) =>
        {
            FlushPending();
            direct.WhenTransitionEnds(element, expectedType, explicitTimeout, () =>
            {
                resolve();
                ApplyPending();
            });
        },
        // FLIP reads/writes force a flush so getBoundingClientRect and the clone read see committed DOM;
        // batching the FLIP position pass into one read/write phase is the separate #163.
        MeasurePosition = element =>
        {
            FlushPending();
            return direct.MeasurePosition(element);
        },
        SetMoveTransform = (element, deltaX, deltaY) =>
        {
            FlushPending();
            direct.SetMoveTransform(element, deltaX, deltaY);
        },
        ClearMoveStyles = element =>
        {
            FlushPending();
            direct.ClearMoveStyles(element);
        },
        HasCssTransform = (element, root, moveClass) =>
        {
            FlushPending();
            return direct.HasCssTransform(element, root, moveClass);
        },
        WhenMoveEnds = (element, resolve) =>
        {
            FlushPending();
            direct.WhenMoveEnds(element, () =>
            {
                resolve();
                ApplyPending();
            });
        },
    };

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
