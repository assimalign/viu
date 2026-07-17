using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The single <c>[JSExport]</c> dispatch entry point of the event system ([V01.01.04.03]): the
/// bridge's one JS listener per (element, event, options) forwards the complete typed payload
/// as primitives in this one call — no per-field <c>JSObject</c> reads, no proxy retained per
/// event — and applies the returned flags (bit 0 <c>stopPropagation</c>, bit 1
/// <c>preventDefault</c>) to the live event.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class BrowserEventDispatch
{
    [JSExport]
    internal static int DispatchBrowserEvent(
        int nodeHandle,
        string eventName,
        bool capture,
        double timeStamp,
        string key,
        string code,
        int modifierFlags,
        int button,
        int buttons,
        double clientX,
        double clientY,
        int detail,
        bool isSelfTarget,
        string? targetValue,
        bool targetChecked)
    {
        var browserEvent = new BrowserEvent(
            eventName,
            timeStamp,
            key,
            code,
            (BrowserEventModifiers)modifierFlags,
            button,
            buttons,
            clientX,
            clientY,
            detail,
            isSelfTarget,
            targetValue,
            targetChecked);
        return BrowserNodeOperations.DispatchEvent(nodeHandle, capture, browserEvent);
    }
}
