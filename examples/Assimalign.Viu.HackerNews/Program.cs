using System;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Router;
using Assimalign.Viu.Router.Browser;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Store;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The browser bootstrap — the one place that touches browser-only interop, so it (and only it) is
/// marked <see cref="SupportedOSPlatformAttribute">browser</see>. Every other type in the app stays
/// platform-neutral and testable. Bootstrap composes the whole Wave-4 surface: the DOM runtime, the
/// router with its click bridge (<see cref="RouterLinkDomBridge"/>), the Pinia-style store registry,
/// and the injectable data client.
/// </summary>
internal static class Program
{
    [SupportedOSPlatform("browser")]
    internal static async Task Main()
    {
        // Initialize the router's browser history, then enable client-side navigation (maps real DOM
        // clicks on RouterLinks into router.Push, suppressing full reloads) before mounting. The Viu
        // DOM bridge itself is loaded inside the app's MountAsync path — no separate init call.
        await RouterHistory.InitializeAsync();
        RouterLinkDomBridge.Install();

        // The injectable data seam, bound to the live HackerNews Firebase API (fetch-backed in WASM).
        var httpClient = new HttpClient { BaseAddress = HackerNewsClient.BaseAddress };
        var stores = new HackerNewsStores(new HackerNewsClient(httpClient));

        // Web history = clean deep-link URLs (/item/8863). Route table + root redirect are shared with tests.
        var history = RouterHistory.CreateWeb();
        // Fully qualified: the simple name Router binds to the Assimalign.Viu.Router namespace here.
        var router = new Assimalign.Viu.Router.Router(history, AppRoutes.Create());
        router.BeforeEach(AppRoutes.RedirectRoot);

        // Run the initial navigation through the full guard pipeline before mounting ([V01.01.08.07],
        // #219): the router starts at the START sentinel, so ReadyAsync navigates to the current URL
        // with from = START and the RedirectRoot beforeEach fires even for a page loaded directly at
        // "/", redirecting it to /top. Awaiting settles the first route so the initial render is correct.
        await router.ReadyAsync();

        // Compose the app with bring-your-own dependency injection over System.IServiceProvider
        // ([V01.01.03.24]): the store registry and the router register through the services surface
        // (AddStore/AddRouter also keep the plugin/provide parity so UseStore() and RouterView resolve
        // either way), and the store-definitions container is a plain app-level service singleton the
        // views resolve with GetRequiredService<HackerNewsStores>(). Component-tree provide/inject stays
        // available for Vue-semantic wiring; this is app-level singleton wiring, now idiomatic .NET DI.
        var registry = new StoreRegistry();
        var builder = BrowserApplication.CreateBuilder(AppShell.Instance);
        builder.AddStore(registry);
        builder.AddRouter(router);
        builder.Services.AddSingleton(stores);
        await builder.Build().MountAsync("#app");

        // Keep the WASM main loop alive; rendering is reactive from here.
        await Task.Delay(Timeout.Infinite);
    }
}
