using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.State;

/// <summary>
/// Identifies and activates one lazily materialized state-store type.
/// </summary>
/// <typeparam name="TStore">The store type.</typeparam>
public sealed class StateStoreDefinition<TStore>
    where TStore : class
{
    private readonly StateStoreActivator<TStore> _activator;

    /// <summary>Initializes a stable store definition.</summary>
    /// <param name="identifier">The non-empty diagnostic identifier.</param>
    /// <param name="activator">The AOT-safe store activator.</param>
    public StateStoreDefinition(string identifier, StateStoreActivator<TStore> activator)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        ArgumentNullException.ThrowIfNull(activator);
        Identifier = identifier;
        _activator = activator;
    }

    /// <summary>Gets the diagnostic identifier.</summary>
    public string Identifier { get; }

    /// <summary>Gets the registry-owned store for this definition.</summary>
    /// <param name="registry">The explicit registry owner.</param>
    /// <returns>The existing or newly materialized store.</returns>
    public TStore Use(IStateStoreRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.GetOrCreate(this);
    }

    /// <summary>
    /// Gets the store for one mounted component by resolving the registry through the context's
    /// application services, then the ambient active registry — the seam every convention uses.
    /// The context is never cast and no bridge interface exists.
    /// </summary>
    /// <param name="context">The mounted component's authoring surface.</param>
    /// <returns>The existing or newly materialized store.</returns>
    public TStore Use(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var registry =
            context.Services?.GetService(typeof(IStateStoreRegistry)) as IStateStoreRegistry
            ?? StateStores.ActiveRegistry
            ?? throw new InvalidOperationException(
                "No state-store registry is reachable: register IStateStoreRegistry in the "
                + "application services or assign StateStores.ActiveRegistry.");
        return registry.GetOrCreate(this);
    }

    internal TStore Activate(IStateContext context) => _activator(context);
}
