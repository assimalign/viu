using System;

namespace Assimalign.Viu.State;

/// <summary>
/// Owns one independently composed set of lazily created state stores.
/// </summary>
public interface IStateStoreRegistry : IDisposable
{
    /// <summary>Gets the number of materialized stores.</summary>
    int Count { get; }

    /// <summary>Gets whether the registry has released every store and store scope.</summary>
    bool IsDisposed { get; }

    /// <summary>Gets an existing store or creates it exactly once for this registry.</summary>
    /// <typeparam name="TStore">The store type.</typeparam>
    /// <param name="definition">The stable store definition.</param>
    /// <returns>The registry-owned store.</returns>
    TStore GetOrCreate<TStore>(StateStoreDefinition<TStore> definition)
        where TStore : class;

    /// <summary>Removes and disposes one materialized store when present.</summary>
    /// <typeparam name="TStore">The store type.</typeparam>
    /// <param name="definition">The stable store definition.</param>
    /// <returns>True when a materialized store was removed.</returns>
    bool Remove<TStore>(StateStoreDefinition<TStore> definition)
        where TStore : class;
}
