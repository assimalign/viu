using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.State;

/// <summary>
/// Defines an AOT-safe setup-style state store and resolves one instance per state registry.
/// </summary>
/// <typeparam name="TStore">The state store type.</typeparam>
public sealed class StateStoreDefinition<TStore>
    where TStore : class
{
    /// <summary>Creates a state store definition.</summary>
    /// <param name="key">The application-unique store key.</param>
    /// <param name="setup">The explicit store setup delegate.</param>
    public StateStoreDefinition(string key, StateStoreSetup<TStore> setup)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(setup);
        Key = key;
        Setup = setup;
    }

    /// <summary>Gets the application-unique store key.</summary>
    public string Key { get; }

    /// <summary>Gets the explicit AOT-safe setup delegate.</summary>
    public StateStoreSetup<TStore> Setup { get; }

    /// <summary>
    /// Resolves the state store from an explicit registry, creating it on first use.
    /// </summary>
    /// <param name="registry">The registry that owns the instance.</param>
    /// <param name="owner">The optional component owner for an explicitly scoped registry.</param>
    /// <returns>The registry-scoped state store instance.</returns>
    public TStore Use(
        IStateStoreRegistry registry,
        IComponentContext? owner = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.GetOrCreate(this, owner);
    }

    /// <summary>
    /// Resolves the state store from a component context carrying
    /// <see cref="IStateStoreContext"/>.
    /// </summary>
    /// <remarks>
    /// The component is used only to locate the application registry and is not recorded as the
    /// owner of an application-global store. For an explicitly isolated feature registry, call
    /// <see cref="Use(IStateStoreRegistry,IComponentContext)"/> and pass the owner deliberately.
    /// </remarks>
    /// <param name="context">The current component context.</param>
    /// <returns>The application-scoped state store instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// The component context does not expose a configured state registry.
    /// </exception>
    public TStore Use(IComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is not IStateStoreContext { State: { } registry })
        {
            throw new InvalidOperationException(
                $"No state registry is available to resolve state store \"{Key}\". "
                + "Configure State on the application or pass an explicit registry.");
        }

        return registry.GetOrCreate(this);
    }

    /// <summary>
    /// Resolves the state store from <see cref="StateStores.ActiveRegistry"/>.
    /// </summary>
    /// <remarks>
    /// This ambient form is intended for browser bootstrap and tests. Server and multi-request
    /// hosts should pass the request-owned registry explicitly.
    /// </remarks>
    /// <returns>The active registry's state store instance.</returns>
    /// <exception cref="InvalidOperationException">No active registry is configured.</exception>
    public TStore Use()
    {
        IStateStoreRegistry registry = StateStores.ActiveRegistry
            ?? throw new InvalidOperationException(
                $"No active state registry is available to resolve state store \"{Key}\". "
                + "Pass a registry explicitly or call StateStores.SetActiveRegistry(...).");
        return registry.GetOrCreate(this);
    }

    /// <summary>Stops and forgets this state store in an explicit registry.</summary>
    /// <param name="registry">The registry holding the state store.</param>
    /// <returns>True when the initialized state store existed and was removed.</returns>
    public bool Dispose(IStateStoreRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.Remove(this);
    }
}
