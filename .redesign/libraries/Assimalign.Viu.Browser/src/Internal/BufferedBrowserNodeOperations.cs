using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The Browser <see cref="RendererOptions{TNode}"/> implementation. Every write operation—node
/// creation, insertion, removal, text, properties, directives, events, and transitions—encodes
/// into a <see cref="DomCommandBuffer"/>. The complete frame applies in one interop call at the
/// renderer commit boundary. Specified by <c>[RND-HOST-1]</c>, <c>[RND-IO-1]</c>, and
/// <c>[EXE-13]</c>.
/// <para>
/// <b>Reads force a flush.</b> Ops that genuinely return data — <c>parentNode</c>,
/// <c>nextSibling</c> and <c>insertStaticContent</c> — cannot be answered from the
/// unapplied buffer, so they commit the pending batch first (one apply call) and then read the live
/// bridge, folding the returned handle into the .NET counter so the two handle allocators never
/// collide. Handles for created nodes are pre-allocated from <see cref="DomCommandBuffer.AllocateHandle"/>
/// (the create op is one-way and cannot return the JS id).
/// </para>
/// <para>
/// <b>Released handles</b> from <c>remove</c>/<c>setElementText</c> are not returned per op; the JS
/// applier collects every handle it releases while draining the batch and returns them from the single
/// apply call, which purges the invoker registry once through
/// <see cref="BrowserEventInvokerRegistry.PurgeReleasedHandles"/>. Specified by
/// <c>[EXE-14]</c>.
/// </para>
/// <para>
/// Transition class writes, reflow barriers, and FLIP transforms share this command stream.
/// Timing and layout reads first flush pending writes, while next-frame and completion callbacks
/// commit the writes they produce. This preserves the from/active/reflow/to class ordering a CSS
/// transition depends on without turning each class mutation into an interop call.
/// </para>
/// Ambient by activation: <see cref="Activate"/> points event dispatch, directive operations, and
/// transition operations at this instance and <see cref="Deactivate"/> restores them.
/// <see cref="RendererOptions{TNode}.Commit"/> integrates buffered writes with synchronous renders,
/// reactive rerenders, post-render hooks, and <see cref="Scheduler.NextTick"/>. Single active
/// buffered renderer per process (single-threaded JS event-loop model); not thread-safe.
/// </summary>
internal sealed class BufferedBrowserNodeOperations
{
    private static BufferedBrowserNodeOperations? ActiveOperations;

    private readonly DomCommandBuffer _buffer = new();
    private readonly Dictionary<int, QualifiedName> _elementNames = [];
    private readonly BrowserEventInvokerRegistry _invokers;
    private readonly BrowserPropertyLeafOperations _leaf;
    private readonly Func<byte[], int, int[]> _applier;
    private readonly Func<string, int>? _querySelector;
    private readonly Func<int, int> _parentNode;
    private readonly Func<int, int> _nextSibling;
    private readonly Func<string, int, int, string?, (int First, int Last)>? _insertStaticContent;
    private readonly Func<int, string>? _snapshotHydration;

    private BrowserEventInvokerRegistry? _previousInvokerRegistry;
    private BrowserDirectiveOperations? _previousDirectiveOperations;
    private DomTransitionOperations? _previousTransitionOperations;
    private bool _isActive;
    private int _interopCallCount;

    internal BufferedBrowserNodeOperations(
        Func<byte[], int, int[]> applier,
        Func<string, int>? querySelector,
        Func<int, int> parentNode,
        Func<int, int> nextSibling,
        Func<string, int, int, string?, (int First, int Last)>? insertStaticContent,
        Func<int, string>? snapshotHydration = null)
    {
        _applier = applier;
        _querySelector = querySelector;
        _parentNode = parentNode;
        _nextSibling = nextSibling;
        _insertStaticContent = insertStaticContent;
        _snapshotHydration = snapshotHydration;
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

    /// <summary>The invoker registry buffered listener operations register into.</summary>
    internal BrowserEventInvokerRegistry Invokers => _invokers;

    /// <summary>The command buffer (diagnostics/tests).</summary>
    internal DomCommandBuffer Buffer => _buffer;

    /// <summary>Gets or sets the sink for synchronous and observed asynchronous event faults.</summary>
    internal Action<Exception> ErrorSink
    {
        get => _invokers.ErrorSink;
        set => _invokers.ErrorSink = value;
    }

    /// <summary>Builds the buffered <see cref="RendererOptions{TNode}"/> for the renderer.</summary>
    internal RendererOptions<int> Create() => new()
    {
        Insert = (child, parent, anchor) => _buffer.WriteInsert(parent, child, anchor),
        Remove = child => _buffer.WriteRemove(child),
        CreateElement = name =>
        {
            int handle = _buffer.AllocateHandle();
            _elementNames[handle] = name;
            _buffer.WriteCreateElement(
                handle,
                name.LocalName,
                BrowserNamespacePolicy.Resolve(name));
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
        ParentNode = node => Read(_parentNode, node),
        NextSibling = node => Read(_nextSibling, node),
        Commit = ApplyPending,
        PatchAttribute = (element, previousBinding, nextBinding) =>
            BrowserPropertyPatcher.Patch(
                _leaf,
                element,
                GetElementName(element),
                previousBinding,
                nextBinding),
        ResolveTeleportTarget = _querySelector is null ? null : ResolveTeleportTarget,
        InsertStaticContent = _insertStaticContent is null ? null : InsertStaticContent,
        CreateHydrationReader = _snapshotHydration is null
            ? null
            : CreateHydrationReader,
    };

    /// <summary>
    /// Commits the pending batch: finalize the frame, hand it across the boundary in one call, purge
    /// the invoker delegates the applier reports released, and reset the frame for the next flush.
    /// A no-op when nothing is buffered.
    /// </summary>
    internal void ApplyPending()
    {
        if (!_buffer.HasPendingOperations)
        {
            return;
        }
        int length = _buffer.FinalizeFrame();
        _interopCallCount++;
        int[] released = _applier(_buffer.BackingArray, length);
        _buffer.ResetFrame();
        _invokers.PurgeReleasedHandles(released);
        for (int index = 0; index < released.Length; index++)
        {
            _elementNames.Remove(released[index]);
        }
    }

    /// <summary>
    /// Folds a handle the bridge issued for a node outside the buffer (a mount container, or a
    /// <c>parentNode</c>/<c>nextSibling</c> result) into the .NET handle counter so a later buffered
    /// create never reuses it. The buffered app observes its mount container here before rendering.
    /// </summary>
    /// <param name="handle">The externally issued handle.</param>
    internal void ObserveForeignHandle(int handle) => _buffer.ObserveForeignHandle(handle);

    /// <summary>Dispatches one DOM-less event through this host's own invoker registry.</summary>
    internal int DispatchEvent(
        int nodeHandle,
        bool capture,
        BrowserEvent browserEvent) =>
        _invokers.Dispatch(nodeHandle, capture, browserEvent);

    /// <summary>Buffers a pre-mount container reset into the next renderer commit.</summary>
    internal void ClearElement(int handle) => _buffer.WriteSetElementText(handle, string.Empty);

    /// <summary>
    /// Exclusively points Browser event, directive, and transition operations at this instance
    /// until the returned lease is disposed.
    /// </summary>
    [SupportedOSPlatform("browser")]
    internal IDisposable Activate()
    {
        if (_isActive)
        {
            throw new InvalidOperationException(
                "This Browser renderer host already owns the active operation lease.");
        }

        if (ActiveOperations is not null)
        {
            throw new InvalidOperationException(
                "Only one Browser renderer host can be active on the single-threaded DOM bridge.");
        }

        _previousInvokerRegistry = BrowserNodeOperations.OverrideInvokerRegistry;
        _previousDirectiveOperations = BrowserDirectiveOperations.Current;
        _previousTransitionOperations = DomTransitionOperations.Current;
        DomTransitionOperations transitionOperations =
            BuildBufferedTransitionOperations(
                _previousTransitionOperations
                    ?? throw new InvalidOperationException(
                        "No Browser transition operations are installed."));
        BrowserDirectiveOperations directiveOperations =
            BuildBufferedDirectiveOperations();
        BrowserNodeOperations.OverrideInvokerRegistry = _invokers;
        DomTransitionOperations.Current = transitionOperations;
        BrowserDirectiveOperations.Current = directiveOperations;
        ActiveOperations = this;
        _isActive = true;
        return new ActivationLease(this);
    }

    private BrowserDirectiveOperations BuildBufferedDirectiveOperations()
    {
        return new BrowserDirectiveOperations
        {
            SetModelListener = (element, rawPropertyName, handler) =>
                _invokers.SetModelListener(element, rawPropertyName, handler),
            SetValueGuarded = _leaf.SetValueGuarded,
            SetBooleanProperty = _leaf.SetBooleanProperty,
            SetStyleProperty = _leaf.SetStyleProperty,
            RemoveStyleProperty = _leaf.RemoveStyleProperty,
            SetCssVariables = (element, names, values) =>
            {
                for (int index = 0; index < names.Length; index++)
                {
                    _leaf.SetStyleProperty(
                        element,
                        names[index],
                        values[index],
                        false);
                }
            },
        };
    }

    /// <summary>Restores ambient operations after the exclusive activation lease ends.</summary>
    [SupportedOSPlatform("browser")]
    private void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;
        BrowserNodeOperations.OverrideInvokerRegistry = _previousInvokerRegistry;
        BrowserDirectiveOperations.Current = _previousDirectiveOperations;
        DomTransitionOperations.Current = _previousTransitionOperations;
        ActiveOperations = null;
    }

    private DomTransitionOperations BuildBufferedTransitionOperations(
        DomTransitionOperations direct)
    {
        return new DomTransitionOperations
        {
            AddTransitionClass = (element, cssClass) =>
                _buffer.WriteAddTransitionClass(element, cssClass),
            RemoveTransitionClass = (element, cssClass) =>
                _buffer.WriteRemoveTransitionClass(element, cssClass),
            ForceReflow = () =>
                _buffer.WriteForceReflow(),
            NextFrame = callback =>
                direct.NextFrame(() =>
                {
                    callback();
                    ApplyPending();
                }),
            WhenTransitionEnds =
                (element, expectedType, explicitTimeout, resolve) =>
                {
                    FlushPending();
                    direct.WhenTransitionEnds(
                        element,
                        expectedType,
                        explicitTimeout,
                        () =>
                        {
                            resolve();
                            ApplyPending();
                        });
                },
            MeasurePositions = handles =>
            {
                FlushPending();
                return direct.MeasurePositions(handles);
            },
            SetMoveTransform = (element, deltaX, deltaY) =>
                _buffer.WriteSetMoveTransform(
                    element,
                    deltaX,
                    deltaY),
            ClearMoveStyles = element =>
                _buffer.WriteClearMoveStyles(element),
            ParentNode = element =>
                Read(_parentNode, element),
            HasCssTransform = (element, root, moveClass) =>
            {
                FlushPending();
                return direct.HasCssTransform(
                    element,
                    root,
                    moveClass);
            },
            WhenMoveEnds = (element, resolve) =>
            {
                FlushPending();
                direct.WhenMoveEnds(
                    element,
                    () =>
                    {
                        resolve();
                        ApplyPending();
                    });
            },
        };
    }

    /// <summary>
    /// Creates the production instance over the real bridge. Reads force a flush and then hit the
    /// live bridge; the applier is the single memory-view interop call. The application activates
    /// the instance only for its mounted lifetime.
    /// </summary>
    [SupportedOSPlatform("browser")]
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
            },
            static container => BrowserDomBridge.SnapshotHydration(container));
        return operations;
    }

    private int Read(Func<int, int> read, int node)
    {
        FlushPending();
        var handle = read(node);
        _buffer.ObserveForeignHandle(handle);
        return handle;
    }

    private int ResolveTeleportTarget(string selector)
    {
        FlushPending();
        int handle = _querySelector!(selector);
        _buffer.ObserveForeignHandle(handle);
        return handle;
    }

    private (int First, int Last) InsertStaticContent(
        MarkupFormat format,
        string content,
        int parent,
        int anchor)
    {
        FlushPending();
        string? elementNamespace = format == MarkupFormat.ExtensibleMarkupLanguage
            && _elementNames.TryGetValue(parent, out QualifiedName parentName)
            ? BrowserNamespacePolicy.Resolve(parentName)
            : null;
        (int first, int last) = _insertStaticContent!(
            content,
            parent,
            anchor,
            elementNamespace);
        _buffer.ObserveForeignHandle(first);
        _buffer.ObserveForeignHandle(last);
        return (first, last);
    }

    private BrowserHydrationReader CreateHydrationReader(int container)
    {
        FlushPending();
        BrowserHydrationReader reader =
            new(_snapshotHydration!(container));
        _buffer.ObserveForeignHandle(reader.MaximumHandle);
        reader.CopyElementNamesTo(_elementNames);
        return reader;
    }

    private QualifiedName GetElementName(int element)
    {
        return _elementNames.TryGetValue(element, out QualifiedName name)
            ? name
            : throw new InvalidOperationException(
                $"Browser element handle '{element}' has no registered qualified name.");
    }

    private void FlushPending()
    {
        if (_buffer.HasPendingOperations)
        {
            ApplyPending();
        }
    }

    [SupportedOSPlatform("browser")]
    private sealed class ActivationLease : IDisposable
    {
        private BufferedBrowserNodeOperations? _owner;

        internal ActivationLease(BufferedBrowserNodeOperations owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            BufferedBrowserNodeOperations? owner = _owner;
            if (owner is null)
            {
                return;
            }

            _owner = null;
            owner.Deactivate();
        }
    }
}
