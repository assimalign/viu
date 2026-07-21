using System.Runtime.Versioning;
using System.Threading.Tasks;

using Assimalign.Viu.Forms.Components;
using Assimalign.Viu.Browser;

namespace Assimalign.Viu.Forms;

/// <summary>
/// The browser bootstrap for the forms sample — the only browser-only code in the app. Builds and
/// mounts the root form component, mirroring Vue's <c>createApp(App).mount('#app')</c> in the .NET
/// builder shape; <c>MountAsync</c> loads the browser bridge inside the mount path (no separate
/// initialization call).
/// </summary>
[SupportedOSPlatform("browser")]
internal static class FormsBootstrap
{
    /// <summary>Builds and mounts the form into <c>#app</c>.</summary>
    /// <returns>A task that completes once the app is mounted.</returns>
    public static async Task RunAsync()
    {
        await BrowserApplication.CreateBuilder(new RegistrationFormComponent()).Build().MountAsync("#app");
    }
}
