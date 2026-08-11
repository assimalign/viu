using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.State;

/// <summary>
/// Defines AOT-safe setup metadata that resolves to one mutable store instance per registry.
/// Specified by <c>[STA-1]</c> through <c>[STA-4]</c>.
/// </summary>
/// <typeparam name="TStore">The store type.</typeparam>
public sealed class StateStoreDefinition<TStore>
    where TStore : class
{
    /// <summary>
    /// Creates reusable metadata for a state store. The delegate is invoked directly inside the
    /// registry-owned child scope; the key is diagnostic and collision metadata, not a runtime
    /// activation token. Specified by <c>[STA-1]</c>.
    /// </summary>
    /// <param name="key">The non-empty application-unique state-store key.</param>
    /// <param name="setup">The explicit AOT-safe store setup delegate.</param>
    public StateStoreDefinition(string key, StateStoreActivator<TStore> setup)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(setup);
        Key = key;
        Setup = setup;
    }

    /// <summary>
    /// Creates reusable metadata for a serializable state store. The serializer is invoked only
    /// for registry payload capture and restore and must remain reflection-free. Specified by
    /// <c>[STA-9]</c> and <c>[EXE-4]</c>.
    /// </summary>
    /// <param name="key">The non-empty application-unique state-store key.</param>
    /// <param name="setup">The explicit AOT-safe store setup delegate.</param>
    /// <param name="serializer">The explicit AOT-safe state serializer.</param>
    public StateStoreDefinition(
        string key,
        StateStoreActivator<TStore> setup,
        IStateStoreSerializer<TStore> serializer)
        : this(key, setup)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        Serializer = serializer;
    }

    /// <summary>
    /// Gets the application-unique store key used for ordinal collision detection. Specified by
    /// <c>[STA-1]</c> and <c>[STA-2]</c>.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the stable diagnostic identifier for this definition. It is identical to
    /// <see cref="Key"/> and is never used for reflection-backed activation. Specified by
    /// <c>[STA-1]</c>.
    /// </summary>
    public string Identifier => Key;

    /// <summary>
    /// Gets the explicit AOT-safe setup delegate invoked inside the registry-owned child scope.
    /// Specified by <c>[STA-1]</c>.
    /// </summary>
    public StateStoreActivator<TStore> Setup { get; }

    /// <summary>
    /// Gets the optional explicit serializer used by registry payload capture and restore. A
    /// materialized definition without one fails actionably when a payload operation includes it.
    /// </summary>
    /// <remarks>Specified by <c>[STA-9]</c> and constrained by <c>[EXE-4]</c>.</remarks>
    public IStateStoreSerializer<TStore>? Serializer { get; }

    /// <summary>
    /// Gets the registry-owned store for this definition, creating it on first use. Different
    /// registries always own different instances. Specified by <c>[STA-2]</c>.
    /// </summary>
    /// <param name="registry">The explicit registry owner.</param>
    /// <returns>The existing or newly materialized store.</returns>
    public TStore Use(IStateStoreRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.GetOrCreate(this);
    }

    /// <summary>
    /// Gets the store from <see cref="StateStores.ActiveRegistry"/>. This ambient form is intended
    /// for single-application browser bootstrap and tests; request-oriented hosts should pass an
    /// explicit registry. Specified by <c>[STA-4]</c>.
    /// </summary>
    /// <returns>The active registry's existing or newly materialized store.</returns>
    /// <exception cref="InvalidOperationException">No ambient registry is active.</exception>
    public TStore Use()
    {
        IStateStoreRegistry registry = StateStores.ActiveRegistry
            ?? throw new InvalidOperationException(
                $"No active state registry is available to resolve state store \"{Key}\". "
                + "Pass a registry explicitly or call StateStores.SetActiveRegistry(...).");
        return registry.GetOrCreate(this);
    }

    /// <summary>
    /// Gets the store for one mounted component by resolving the registry through the context's
    /// application services, then the ambient active registry. The component is neither type-tested
    /// nor retained, so application-global setup cannot depend on mount order. Specified by
    /// <c>[STA-3]</c>, <c>[STA-4]</c>, and <c>[CMP-33]</c>.
    /// </summary>
    /// <param name="context">The mounted component's authoring surface.</param>
    /// <returns>The existing or newly materialized store.</returns>
    public TStore Use(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        object? service = context.Services?.GetService(typeof(IStateStoreRegistry));
        IStateStoreRegistry? registry = service switch
        {
            IStateStoreRegistry configuredRegistry => configuredRegistry,
            _ => StateStores.ActiveRegistry,
        };

        if (registry is null)
        {
            throw new InvalidOperationException(
                $"No state registry is available to resolve state store \"{Key}\". Register "
                + "IStateStoreRegistry in the application services, make a registry active, or "
                + "pass one explicitly.");
        }

        return registry.GetOrCreate(this);
    }

    /// <summary>
    /// Stops and forgets this definition's store in an explicit registry. Specified by
    /// <c>[STA-2]</c>.
    /// </summary>
    /// <param name="registry">The registry that may own the store.</param>
    /// <returns><see langword="true"/> when a materialized store was removed.</returns>
    public bool Remove(IStateStoreRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.Remove(this);
    }
}
