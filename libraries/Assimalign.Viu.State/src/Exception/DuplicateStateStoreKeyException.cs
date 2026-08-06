using System;

namespace Assimalign.Viu.State;

/// <summary>
/// Raised when two different state-store definitions claim the same key in one registry.
/// </summary>
public sealed class DuplicateStateStoreKeyException : Exception
{
    /// <summary>Creates an exception for the duplicated <paramref name="stateStoreKey"/>.</summary>
    /// <param name="stateStoreKey">The state-store key already owned by another definition.</param>
    public DuplicateStateStoreKeyException(string stateStoreKey)
        : base(
            $"A different state store is already registered under key \"{stateStoreKey}\" "
            + "in this registry. Reuse the original definition or choose a distinct key.")
    {
        StateStoreKey = stateStoreKey;
    }

    /// <summary>Gets the state-store key that collided.</summary>
    public string StateStoreKey { get; }
}
