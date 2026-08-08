using System;

namespace Assimalign.Viu.State;

/// <summary>
/// Raised when two different state-store definitions claim the same ordinal key in one registry.
/// Specified by <c>[STA-2]</c>.
/// </summary>
public sealed class DuplicateStateStoreKeyException : Exception
{
    /// <summary>
    /// Creates an exception for <paramref name="stateStoreKey"/> without inspecting either store
    /// type or setup delegate.
    /// </summary>
    /// <param name="stateStoreKey">The key already owned by another definition.</param>
    public DuplicateStateStoreKeyException(string stateStoreKey)
        : base(
            $"A different state store is already registered under key \"{stateStoreKey}\" "
            + "in this registry. Reuse the original definition or choose a distinct key.")
    {
        ArgumentException.ThrowIfNullOrEmpty(stateStoreKey);
        StateStoreKey = stateStoreKey;
    }

    /// <summary>Gets the ordinal state-store key that collided.</summary>
    public string StateStoreKey { get; }
}
