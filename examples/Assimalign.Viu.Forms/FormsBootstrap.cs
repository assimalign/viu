using System.Runtime.Versioning;
using System.Threading.Tasks;

using Assimalign.Viu.Forms.Components;
using Assimalign.Viu.RuntimeDom;

namespace Assimalign.Viu.Forms;

/// <summary>
/// The browser bootstrap for the forms sample — the only browser-only code in the app. Initializes
/// the DOM bridge and mounts the root form component, mirroring Vue's <c>createApp(App).mount('#app')</c>.
/// </summary>
[SupportedOSPlatform("browser")]
internal static class FormsBootstrap
{
    /// <summary>Initializes the runtime and mounts the form into <c>#app</c>.</summary>
    /// <returns>A task that completes once the app is mounted.</returns>
    public static async Task RunAsync()
    {
        await BrowserRuntime.InitializeAsync();
        BrowserRuntime.CreateApp(new RegistrationFormComponent()).Mount("#app");
    }
}
