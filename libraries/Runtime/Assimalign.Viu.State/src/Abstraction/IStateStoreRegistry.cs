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
    /// Registers a plugin for future store creations. Registrations run in order, are snapshotted
    /// before setup begins, and never revisit materialized stores. Specified by <c>[STA-10]</c>.
    /// </summary>
    /// <param name="plugin">
    /// The externally owned plugin to run inside each future store's scope. The registry does not
    /// dispose plugin instances; extensions are separately owned by their store scope.
    /// </param>
    /// <returns>This registry for further composition.</returns>
    /// <exception cref="NotSupportedException">
    /// A custom registry has not opted into plugin support by implementing this member.
    /// </exception>
    IStateStoreRegistry Use(IStateStorePlugin plugin)
        => throw new NotSupportedException("This state store registry does not support plugins.");

    /// <summary>
    /// Gets a live store's extension by its exact declared type key. The store must belong to this
    /// registry; assignable but differently keyed values are not considered. Specified by
    /// <c>[STA-10]</c>.
    /// </summary>
    /// <typeparam name="TExtension">The statically selected extension key and value type.</typeparam>
    /// <param name="store">The exact registry-owned store instance.</param>
    /// <returns>The extension registered under this exact type key.</returns>
    /// <exception cref="InvalidOperationException">The store is not owned or the type key is absent.</exception>
    /// <exception cref="ObjectDisposedException">The registry or store scope has stopped.</exception>
    /// <exception cref="NotSupportedException">
    /// A custom registry has not opted into extension support by implementing this member.
    /// </exception>
    TExtension GetExtension<TExtension>(object store)
        where TExtension : notnull
        => throw new NotSupportedException("This state store registry does not support extensions.");

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
