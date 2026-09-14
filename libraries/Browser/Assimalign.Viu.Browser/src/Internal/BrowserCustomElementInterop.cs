using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Assimalign.Viu.Browser;

[SupportedOSPlatform("browser")]
internal static partial class BrowserCustomElementInterop
{
    [JSImport("customElementsBridge.define", BrowserDomBridge.ModuleName)]
    internal static partial void Define(int definitionIdentifier, string tagName, bool useShadowRoot,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] parameterNames,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] attributeNames);

    [JSImport("customElementsBridge.setReady", BrowserDomBridge.ModuleName)]
    internal static partial void SetReady(bool ready);

    [JSImport("customElementsBridge.undefine", BrowserDomBridge.ModuleName)]
    internal static partial void Undefine(int definitionIdentifier);

    [JSImport("customElementsBridge.updateProperties", BrowserDomBridge.ModuleName)]
    internal static partial void UpdateProperties(int elementIdentifier,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] names,
        [JSMarshalAs<JSType.Array<JSType.Any>>] object?[] values);

    [JSImport("customElementsBridge.dispatchEvent", BrowserDomBridge.ModuleName)]
    internal static partial void DispatchEvent(int elementIdentifier, string name,
        [JSMarshalAs<JSType.Array<JSType.Any>>] object?[] arguments);

    [JSImport("customElementsBridge.release", BrowserDomBridge.ModuleName)]
    internal static partial int[] Release(int elementIdentifier);

    [JSImport("customElementsBridge.warn", BrowserDomBridge.ModuleName)]
    internal static partial void Warn(string message);
}
