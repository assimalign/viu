using System.Runtime.Versioning;
using System.Threading.Tasks;

using Assimalign.Viu.Browser;
using Assimalign.Viu.TodoMvc.Components;

namespace Assimalign.Viu.TodoMvc;

/// <summary>
/// The browser bootstrap for the TodoMVC sample — the only browser-only code in the app. Builds the
/// app, provides the shared <see cref="TodoStore"/> app-wide through a typed <c>InjectionKey</c> (the
/// same store the root and each item component inject), and mounts the root. A Viu app bootstrap
/// mirrors Vue's <c>createApp(App).provide(key, store).mount('#app')</c> in the .NET builder shape
/// (https://vuejs.org/guide/scaling-up/state-management.html#simple-state-management-with-reactivity-api);
/// <c>MountAsync</c> loads the browser bridge inside the mount path (no separate initialization call).
/// </summary>
[SupportedOSPlatform("browser")]
internal static class TodoMvcBootstrap
{
    /// <summary>Builds and mounts the TodoMVC app into <c>#app</c>.</summary>
    /// <returns>A task that completes once the app is mounted.</returns>
    public static async Task RunAsync()
    {
        var store = TodoStore.CreateSeeded();
        var builder = BrowserApplication.CreateBuilder(new TodoAppComponent());
        builder.Provide(TodoStore.Key, store);
        await builder.Build().MountAsync("#app");
    }
}
