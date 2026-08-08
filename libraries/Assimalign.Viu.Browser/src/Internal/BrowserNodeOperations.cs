using System;
using System.Runtime.Versioning;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Owns the synchronous Browser bridge primitives used around the command-frame renderer: event
/// dispatch, directive and transition callbacks, pre-mount cleanup, and handle diagnostics.
/// Renderer writes themselves always flow through <see cref="BufferedBrowserNodeOperations"/>.
/// Handles are integers and zero is the no-node sentinel. Event handling uses an invoker registry,
/// so a rerendered handler swaps a managed delegate without listener-management interop. Specified
/// by <c>[EXE-12]</c>, <c>[RND-IO-1]</c>, <c>[RND-IO-2]</c>, and <c>[EXE-14]</c>.
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
            ParentNode = static element =>
                BrowserDomBridge.ParentNode(element),
            HasCssTransform = static (element, root, moveClass) =>
                BrowserDomBridge.HasCssTransform(
                    element,
                    root,
                    moveClass),
            WhenMoveEnds = static (element, resolve) =>
                BrowserDomBridge.WhenMoveEnds(element, resolve),
        };
    }

    /// <summary>
    /// Overrides the invoker registry used by the single <c>[JSExport]</c> dispatch entry
    /// (<see cref="BrowserEventDispatch"/>) and registry diagnostics. Buffered mode
    /// ([V01.01.04.05]) sets this to its own registry — whose add/remove callbacks encode listener
    /// operations into the command buffer rather than call the bridge directly — so live events and
    /// diagnostics both address the handlers registered on the active command-frame renderer. Null
    /// selects the bridge fallback registry. Ambient static (single active renderer per process,
    /// single-threaded JavaScript event-loop model).
    /// </summary>
    internal static BrowserEventInvokerRegistry? OverrideInvokerRegistry;

    internal static int DispatchEvent(int nodeHandle, bool capture, BrowserEvent browserEvent)
        => ActiveInvokerRegistry.Dispatch(nodeHandle, capture, browserEvent);

    /// <summary>The number of listeners in the registry selected by the active renderer.</summary>
    internal static int ActiveInvokerCount => ActiveInvokerRegistry.InvokerCount;

    /// <summary>Routes Browser event faults to an application-owned error handler.</summary>
    internal static Action<Exception> ErrorSink
    {
        set => Invokers.ErrorSink = value;
    }

    /// <summary>Clears an element's content (pre-mount container reset), purging released handles.</summary>
    internal static void ClearElement(int nodeHandle)
        => PurgeReleasedHandles(BrowserDomBridge.SetElementText(nodeHandle, string.Empty));

    /// <summary>Registry sizes for leak diagnostics: JS nodes, JS listener maps, .NET invokers.</summary>
    internal static (int JavaScriptNodes, int JavaScriptListenerMaps, int ManagedListeners) GetRegistryDiagnostics()
    {
        var sizes = BrowserDomBridge.GetRegistrySizes();
        return (sizes[0], sizes[1], ActiveInvokerCount);
    }

    private static BrowserEventInvokerRegistry ActiveInvokerRegistry
        => OverrideInvokerRegistry ?? Invokers;

    private static void PurgeReleasedHandles(int[] releasedHandles) =>
        Invokers.PurgeReleasedHandles(releasedHandles);
}
