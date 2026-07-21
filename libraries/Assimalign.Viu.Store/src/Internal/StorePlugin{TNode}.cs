using System;

using Assimalign.Viu;

namespace Assimalign.Viu.Store;

/// <summary>
/// The <see cref="IPlugin{TNode}"/> adapter that installs a <see cref="StoreRegistry"/> on an app —
/// the C# bridge for Pinia's <c>app.use(pinia)</c> (<c>packages/pinia/src/createPinia.ts</c>
/// <c>pinia.install</c>). Because <see cref="IPlugin{TNode}"/> is generic over the platform node
/// while a registry is platform-agnostic, the registry is wrapped per node type by
/// <see cref="StoreRegistry.AsPlugin{TNode}"/> instead of implementing the plugin interface itself.
/// Installing provides the registry app-wide (so a component <c>Setup</c> can inject it) and makes it
/// the ambient <see cref="Stores.ActiveRegistry"/>. Internal.
/// </summary>
/// <typeparam name="TNode">The app's platform node type.</typeparam>
internal sealed class StorePlugin<TNode> : IPlugin<TNode>
    where TNode : notnull
{
    private readonly StoreRegistry _registry;

    public StorePlugin(StoreRegistry registry) => _registry = registry;

    public void Install(Application<TNode> application, object? options)
    {
        ArgumentNullException.ThrowIfNull(application);
        // Provide the registry app-wide (upstream: app.provide(piniaSymbol, pinia)) so UseStore() in a
        // component Setup injects it, and set it active (upstream: setActivePinia(pinia)) so
        // non-component resolution has a fallback.
        application.Provide(StoreRegistry.InjectionKey, _registry);
        Stores.SetActiveRegistry(_registry);
    }
}
