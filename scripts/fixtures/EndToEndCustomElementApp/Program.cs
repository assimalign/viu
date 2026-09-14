using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;

namespace EndToEndCustomElementApp;

internal static partial class Program
{
    private static BrowserCustomElements? _customElements;
    private static string _emptyRegistry = string.Empty;
    private static int _mountCount;
    private static int _unmountCount;
    private static int _updateCount;

    internal static async Task Main()
    {
        ComponentFactory components = new();
        GeneratedViuComponents.Register(components);
        await BrowserRuntime.InitializeAsync();
        _emptyRegistry = CaptureRegistry();

        // [V01.01.04.08]: the external composition root retains one owner for the page lifetime.
        _customElements = new BrowserCustomElements(components);
        _customElements.Define(ComponentReference.ForName("Counter"), "viu-counter");
        _customElements.Define(
            ComponentReference.ForName("Counter"),
            "viu-light-counter",
            new CustomElementOptions { UseShadowRoot = false });
    }

    internal static void RecordMount() => _mountCount++;

    internal static void RecordUnmount() => _unmountCount++;

    internal static void RecordUpdate() => _updateCount++;

    [JSExport]
    public static string CaptureRegistry()
    {
        (int javaScriptNodes, int javaScriptListenerMaps, int managedListeners) =
            BrowserRuntime.GetRegistryDiagnostics();
        return $"{javaScriptNodes}|{javaScriptListenerMaps}|{managedListeners}";
    }

    [JSExport]
    public static string GetEmptyRegistry() => _emptyRegistry;

    [JSExport]
    public static int GetMountCount() => _mountCount;

    [JSExport]
    public static int GetUnmountCount() => _unmountCount;

    [JSExport]
    public static int GetUpdateCount() => _updateCount;

    [JSExport]
    public static void DisposeOwner() => _customElements?.Dispose();
}
