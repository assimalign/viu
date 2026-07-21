using System;

using Assimalign.Viu;

namespace Assimalign.Viu.Store;

/// <summary>
/// The <see cref="IPlugin"/> adapter that installs a <see cref="StoreRegistry"/> on an app — the C#
/// bridge for Pinia's <c>app.use(pinia)</c> (<c>packages/pinia/src/createPinia.ts</c>
/// <c>pinia.install</c>). A registry is platform-agnostic, so it is wrapped by
/// <see cref="StoreRegistry.AsPlugin"/> rather than implementing the (platform-neutral) plugin
/// interface itself. Installing provides the registry app-wide (so a component <c>Setup</c> can inject
/// it) and makes it the ambient <see cref="Stores.ActiveRegistry"/>. Internal.
/// </summary>
internal sealed class StorePlugin : IPlugin
{
    private readonly StoreRegistry _registry;

    public StorePlugin(StoreRegistry registry) => _registry = registry;

    public void Install(IApplication application, object? options)
    {
        ArgumentNullException.ThrowIfNull(application);
        // Provide the registry app-wide (upstream: app.provide(piniaSymbol, pinia)) so UseStore() in a
        // component Setup injects it, and set it active (upstream: setActivePinia(pinia)) so
        // non-component resolution has a fallback.
        application.Provide(StoreRegistry.InjectionKey, _registry);
        Stores.SetActiveRegistry(_registry);
    }
}
