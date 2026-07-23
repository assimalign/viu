using System;
using System.Runtime.Versioning;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The production <see cref="RendererOptions{TNode}"/> over the DOM bridge — the browser
/// implementation of Vue's <c>nodeOps</c> + <c>patchProp</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/nodeOps.ts). Handles are
/// ints (see the marshaling ADR in <c>libraries/Assimalign.Viu.Browser/docs/</c>); 0 is the
/// "no node" sentinel. Node removal releases the removed subtree's JS handles and DOM
/// listeners deterministically, and the returned handle list purges the invoker registry in
/// the same call — no leaks on either side of the boundary ([V01.01.04.01]). Event handling
/// goes through the invoker registry ([V01.01.04.03]): handler changes between renders swap a
/// delegate with zero listener-management interop.
/// </summary>
[SupportedOSPlatform("browser")]
internal static class BrowserNodeOperations
{
    private static readonly BrowserEventInvokerRegistry Invokers = new(
        static (handle, eventName, once, capture, passive) =>
            BrowserDomBridge.AddEventListener(handle, eventName, once, capture, passive),
        static (handle, eventName, capture) =>
            BrowserDomBridge.RemoveEventListener(handle, eventName, capture));

    private static readonly BrowserPropertyLeafOperations LeafOperations = new()
    {
        SetAttribute = static (element, name, value) => BrowserDomBridge.SetAttribute(element, name, value),
        RemoveAttribute = static (element, name) => BrowserDomBridge.RemoveAttribute(element, name),
        SetXlinkAttribute = static (element, name, value) => BrowserDomBridge.SetXlinkAttribute(element, name, value),
        RemoveXlinkAttribute = static (element, name) => BrowserDomBridge.RemoveXlinkAttribute(element, name),
        SetClassName = static (element, value) => BrowserDomBridge.SetClassName(element, value),
        SetStringProperty = static (element, name, value) => BrowserDomBridge.SetStringProperty(element, name, value),
        SetBooleanProperty = static (element, name, value) => BrowserDomBridge.SetBooleanProperty(element, name, value),
        SetValueGuarded = static (element, value) => BrowserDomBridge.SetValueGuarded(element, value),
        SetStyleText = static (element, cssText) => BrowserDomBridge.SetStyleText(element, cssText),
        SetStyleProperty = static (element, name, value, important) => BrowserDomBridge.SetStyleProperty(element, name, value, important),
        RemoveStyleProperty = static (element, name) => BrowserDomBridge.RemoveStyleProperty(element, name),
        SetEventListener = static (element, rawPropertyName, listener) =>
            Invokers.SetListener(element, rawPropertyName, listener),
    };

    static BrowserNodeOperations()
    {
        BrowserDirectiveOperations.Current = new BrowserDirectiveOperations
        {
            SetModelListener = static (element, rawPropertyName, handler) =>
                Invokers.SetModelListener(element, rawPropertyName, handler),
            SetValueGuarded = LeafOperations.SetValueGuarded,
            SetBooleanProperty = LeafOperations.SetBooleanProperty,
            SetStyleProperty = LeafOperations.SetStyleProperty,
            RemoveStyleProperty = LeafOperations.RemoveStyleProperty,
            SetCssVariables = static (element, names, values) =>
                BrowserDomBridge.SetCssVariables(element, names, values),
        };
        DomTransitionOperations.Current = new DomTransitionOperations
        {
            AddTransitionClass = static (element, cssClass) =>
                BrowserDomBridge.AddTransitionClass(element, cssClass),
            RemoveTransitionClass = static (element, cssClass) =>
                BrowserDomBridge.RemoveTransitionClass(element, cssClass),
            NextFrame = static callback =>
                BrowserDomBridge.NextFrame(callback),
            ForceReflow = static () =>
                BrowserDomBridge.ForceReflow(),
            WhenTransitionEnds =
                static (element, expectedType, explicitTimeout, resolve) =>
                    BrowserDomBridge.WhenTransitionEnds(
                        element,
                        expectedType,
                        explicitTimeout,
                        resolve),
            MeasurePositions = static handles =>
            {
                double[] flat =
                    BrowserDomBridge.MeasurePositions(handles);
                TransitionRectangle[] rectangles =
                    new TransitionRectangle[handles.Length];
                for (int index = 0; index < handles.Length; index++)
                {
                    rectangles[index] = new TransitionRectangle(
                        flat[index * 4],
                        flat[(index * 4) + 1],
                        flat[(index * 4) + 2],
                        flat[(index * 4) + 3]);
                }

                return rectangles;
            },
            SetMoveTransform = static (element, deltaX, deltaY) =>
                BrowserDomBridge.SetMoveTransform(
                    element,
                    deltaX,
                    deltaY),
            ClearMoveStyles = static element =>
                BrowserDomBridge.ClearMoveStyles(element),
            HasCssTransform = static (element, root, moveClass) =>
                BrowserDomBridge.HasCssTransform(
                    element,
                    root,
                    moveClass),
            WhenMoveEnds = static (element, resolve) =>
                BrowserDomBridge.WhenMoveEnds(element, resolve),
        };
    }

    internal static RendererOptions<int> Create() => new()
    {
        Insert = static (child, parent, anchor) => BrowserDomBridge.Insert(parent, child, anchor),
        Remove = static child => Invokers.PurgeReleasedHandles(BrowserDomBridge.Remove(child)),
        CreateElement = static (tag, elementNamespace) => BrowserDomBridge.CreateElement(tag, elementNamespace),
        CreateText = static text => BrowserDomBridge.CreateText(text),
        CreateComment = static text => BrowserDomBridge.CreateComment(text),
        SetText = static (node, text) => BrowserDomBridge.SetText(node, text),
        ParentNode = static node => BrowserDomBridge.ParentNode(node),
        NextSibling = static node => BrowserDomBridge.NextSibling(node),
        PatchAttribute = static (element, elementTag, propertyName, previousValue, nextValue, elementNamespace) =>
            BrowserPropertyPatcher.Patch(LeafOperations, element, elementTag, propertyName, previousValue, nextValue, elementNamespace),
        SetScopeIdentifier = static (element, scopeIdentifier) =>
            BrowserDomBridge.SetAttribute(element, scopeIdentifier, string.Empty),
        ResolveTeleportTarget = static target =>
            target is string selector
                ? BrowserDomBridge.QuerySelector(selector)
                : default,
        InsertStaticContent = static (content, parent, anchor, elementNamespace) =>
        {
            var span = BrowserDomBridge.InsertStaticContent(content, parent, anchor, elementNamespace);
            return (span[0], span[1]);
        },
        CreateHydrationReader = static container =>
            new BrowserHydrationReader(
                BrowserDomBridge.SnapshotHydration(container)),
    };

    /// <summary>
    /// Overrides the invoker registry the single <c>[JSExport]</c> dispatch entry
    /// (<see cref="BrowserEventDispatch"/>) routes to. Buffered mode ([V01.01.04.05]) sets this to its
    /// own registry — whose add/remove callbacks encode listener ops into the command buffer rather
    /// than call the bridge directly — so a live event reaches the handlers registered on the buffered
    /// renderer. Null selects the direct-path registry. Ambient static (single active renderer per
    /// process, single-threaded JS event-loop model).
    /// </summary>
    internal static Func<int, bool, BrowserEvent, int>? OverrideDispatcher;

    internal static int DispatchEvent(int nodeHandle, bool capture, BrowserEvent browserEvent)
        => OverrideDispatcher is { } dispatcher
            ? dispatcher(nodeHandle, capture, browserEvent)
            : Invokers.Dispatch(nodeHandle, capture, browserEvent);

    /// <summary>Routes direct-mode event faults to an application-owned error handler.</summary>
    internal static Action<Exception> ErrorSink
    {
        set => Invokers.ErrorSink = value;
    }

    /// <summary>Clears an element's content (pre-mount container reset), purging released handles.</summary>
    internal static void ClearElement(int nodeHandle)
        => Invokers.PurgeReleasedHandles(BrowserDomBridge.SetElementText(nodeHandle, string.Empty));

    /// <summary>Registry sizes for leak diagnostics: JS nodes, JS listener maps, .NET invokers.</summary>
    internal static (int JsNodes, int JsListenerMaps, int DotnetListeners) GetRegistryDiagnostics()
    {
        var sizes = BrowserDomBridge.GetRegistrySizes();
        return (sizes[0], sizes[1], Invokers.InvokerCount);
    }
}
