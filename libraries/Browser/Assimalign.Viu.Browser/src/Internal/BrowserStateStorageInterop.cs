using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Assimalign.Viu.Browser;

// [STA-11]: native storage errors cross unchanged for the host-neutral plugin to diagnose.
[SupportedOSPlatform("browser")]
internal static partial class BrowserStateStorageInterop
{
    [JSImport("stateStorage.read", BrowserDomBridge.ModuleName)]
    internal static partial string? Read(bool session, string key);

    [JSImport("stateStorage.write", BrowserDomBridge.ModuleName)]
    internal static partial void Write(bool session, string key, string value);

    [JSImport("stateStorage.remove", BrowserDomBridge.ModuleName)]
    internal static partial void Remove(bool session, string key);
}
