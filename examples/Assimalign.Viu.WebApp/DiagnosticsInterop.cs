using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

// Interop for the ?diagnostics=1 mode: the JSObject-marshaling strategy ops (benchmark.js) and
// the SAME int-handle leaf ops the production bridge exports — both driven in identical loops
// so the [V01.01.04.01] marshaling ADR compares strategies fairly.
[SupportedOSPlatform("browser")]
internal static partial class DiagnosticsInterop
{
    // --- helpers ----------------------------------------------------------------------------

    [JSImport("benchmark.getQuery", "benchmark")]
    internal static partial string GetQuery();

    [JSImport("benchmark.reportCrash", "benchmark")]
    internal static partial void ReportCrash(string text);

    // --- JSObject strategy (proxied element references) -------------------------------------

    [JSImport("benchmark.createElementObject", "benchmark")]
    internal static partial JSObject CreateElementObject(string tagName);

    [JSImport("benchmark.setElementTextObject", "benchmark")]
    internal static partial void SetElementTextObject(JSObject element, string text);

    [JSImport("benchmark.setAttributeObject", "benchmark")]
    internal static partial void SetAttributeObject(JSObject element, string name, string value);

    [JSImport("benchmark.insertObject", "benchmark")]
    internal static partial void InsertObject(JSObject parent, JSObject child);

    [JSImport("benchmark.removeObject", "benchmark")]
    internal static partial void RemoveObject(JSObject element);

    // --- int-handle strategy: the production bridge module's own leaf ops ---------------------

    [JSImport("dom.createElement", "Assimalign.Viu.RuntimeDom")]
    internal static partial int CreateElementHandle(string tagName, string? namespaceName);

    [JSImport("dom.setElementText", "Assimalign.Viu.RuntimeDom")]
    internal static partial int[] SetElementTextHandle(int nodeHandle, string text);

    [JSImport("dom.setAttribute", "Assimalign.Viu.RuntimeDom")]
    internal static partial void SetAttributeHandle(int nodeHandle, string name, string value);

    [JSImport("dom.insert", "Assimalign.Viu.RuntimeDom")]
    internal static partial void InsertHandle(int parentHandle, int childHandle, int anchorHandle);

    [JSImport("dom.remove", "Assimalign.Viu.RuntimeDom")]
    internal static partial int[] RemoveHandle(int childHandle);
}
