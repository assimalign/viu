using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.State;

/// <summary>Owns setup-style state stores and their reactive lifetimes for one application.</summary>
public interface IStateStoreRegistry : IDisposable
{
    /// <summary>Gets the number of initialized stores.</summary>
    int Count { get; }

    /// <summary>Gets whether the registry has been disposed.</summary>
    bool IsDisposed { get; }

    /// <summary>Gets or creates one store for the definition's key.</summary>
    /// <typeparam name="TStore">The state store type.</typeparam>
    /// <param name="definition">The shared state store definition.</param>
    /// <param name="owner">The optional component that requested the store.</param>
    /// <returns>The application-scoped store instance.</returns>
    TStore GetOrCreate<TStore>(
        StateStoreDefinition<TStore> definition,
        IComponentContext? owner = null)
        where TStore : class;

    /// <summary>Removes and stops one initialized store.</summary>
    /// <typeparam name="TStore">The state store type.</typeparam>
    /// <param name="definition">The shared state store definition.</param>
    /// <returns>True when the initialized store existed.</returns>
    bool Remove<TStore>(StateStoreDefinition<TStore> definition)
        where TStore : class;
}

