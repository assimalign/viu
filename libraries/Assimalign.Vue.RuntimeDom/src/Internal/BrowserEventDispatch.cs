using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The single <c>[JSExport]</c> dispatch entry point of the event system ([V01.01.04.03]): the
/// bridge's one JS listener per (element, event, options) forwards the complete typed payload
/// as primitives in this one call — no per-field <c>JSObject</c> reads, no proxy retained per
/// event — and applies the returned flags (bit 0 <c>stopPropagation</c>, bit 1
/// <c>preventDefault</c>) to the live event. <paramref name="selectedValues"/> rides the same
/// call for <c>&lt;select multiple&gt;</c> so <c>v-model</c> ([V01.01.04.06]) never issues a
/// per-event follow-up read; it is null for every non-multi-select event.
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
        bool targetChecked,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[]? selectedValues)
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
            targetChecked,
            selectedValues);
        return BrowserNodeOperations.DispatchEvent(nodeHandle, capture, browserEvent);
    }
}
