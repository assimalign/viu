using System;

namespace Assimalign.Viu.Store;

/// <summary>
/// The deterministic error raised when two <em>different</em> <see cref="StoreDefinition{TStore}"/>
/// instances are resolved against the same <see cref="StoreRegistry"/> under the same
/// <see cref="StoreDefinition{TStore}.Id"/>. Store ids are unique per registry: aliasing two
/// definitions onto one id would silently share (or clobber) state, so the second resolution fails
/// here instead. The C# analogue of the uniqueness Pinia relies on when keying stores by id in
/// <c>pinia._s</c> and of its dev-time "store … already used" guard
/// (<c>packages/pinia/src/store.ts</c>). Resolving the <em>same</em> definition twice is not an
/// error — it returns the cached instance.
/// </summary>
public sealed class DuplicateStoreIdException : Exception
{
    /// <summary>Creates a <see cref="DuplicateStoreIdException"/> for <paramref name="storeId"/>.</summary>
    /// <param name="storeId">The store id that a different definition already registered.</param>
    public DuplicateStoreIdException(string storeId)
        : base($"A different store is already registered under id \"{storeId}\" in this registry. "
            + "Store ids must be unique per registry: give the store a distinct id, or reuse the "
            + "original definition instead of defining a second one with the same id.")
    {
        StoreId = storeId;
    }

    /// <summary>The store id that collided.</summary>
    public string StoreId { get; }
}
