using System;

namespace Assimalign.Viu.State;

/// <summary>
/// Owns one independently composed set of lazily created state stores and their reactive
/// lifetimes. Specified by <c>[STA-2]</c> and <c>[STA-3]</c>.
/// </summary>
public interface IStateStoreRegistry : IDisposable
{
    /// <summary>Gets the number of materialized stores that the registry currently owns.</summary>
    int Count { get; }

    /// <summary>
    /// Gets whether the registry has released every store and store scope. Once disposed, the
    /// registry cannot materialize or remove stores.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// Gets the store owned for <paramref name="definition"/>, or creates it exactly once in a
    /// registry-owned child scope. Specified by <c>[STA-2]</c>.
    /// </summary>
    /// <typeparam name="TStore">The store type.</typeparam>
    /// <param name="definition">The stable store definition.</param>
    /// <returns>The registry-owned store.</returns>
    TStore GetOrCreate<TStore>(StateStoreDefinition<TStore> definition)
        where TStore : class;

    /// <summary>
    /// Removes and disposes the store and child scope owned for <paramref name="definition"/> when
    /// present. Specified by <c>[STA-2]</c>.
    /// </summary>
    /// <typeparam name="TStore">The store type.</typeparam>
    /// <param name="definition">The stable store definition.</param>
    /// <returns>True when a materialized store was removed.</returns>
    bool Remove<TStore>(StateStoreDefinition<TStore> definition)
        where TStore : class;
}
