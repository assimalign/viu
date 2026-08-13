using System;

namespace Assimalign.Viu.State;

/// <summary>
/// Carries typed metadata for one state-store subscription notification. Specified by
/// <c>[STA-5]</c> and <c>[STA-7]</c>.
/// </summary>
public readonly struct StateStoreMutation
{
    /// <summary>Creates immutable mutation metadata.</summary>
    /// <param name="stateStoreKey">The key of the state store that changed.</param>
    /// <param name="kind">How the change reached the store.</param>
    public StateStoreMutation(
        string stateStoreKey,
        StateStorePatchKind kind)
    {
        ArgumentException.ThrowIfNullOrEmpty(stateStoreKey);
        StateStoreKey = stateStoreKey;
        Kind = kind;
    }

    /// <summary>Gets the key of the state store that changed.</summary>
    public string StateStoreKey { get; }

    /// <summary>Gets how the change reached the state store.</summary>
    public StateStorePatchKind Kind { get; }
}
