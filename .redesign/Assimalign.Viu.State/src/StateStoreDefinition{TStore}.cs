using System;

namespace Assimalign.Viu.State;

/// <summary>Defines an AOT-safe setup-style state store.</summary>
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
}

