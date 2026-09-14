using System;
using System.Diagnostics.CodeAnalysis;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>
/// Exposes a newly created store and its registry-owned lifetime to a plugin. Typed extensions use
/// explicit type keys without discovering members or changing the store shape. Specified by
/// <c>[STA-10]</c>.
/// </summary>
/// <remarks>This type is not thread-safe and targets Viu's single-threaded event-loop model.</remarks>
public sealed class StateStorePluginContext
{
    internal StateStorePluginContext(
        IStateStoreRegistry registry,
        StateStoreEntry entry,
        IStateContext stateContext,
        string identifier,
        StateStorePersistenceDescriptor? persistence)
    {
        Registry = registry;
        Entry = entry;
        StateContext = stateContext;
        Identifier = identifier;
        Persistence = persistence;
    }

    /// <summary>Gets the registry that owns this store. Specified by <c>[STA-10]</c>.</summary>
    public IStateStoreRegistry Registry { get; }

    /// <summary>Gets the exact instance returned by store setup. Specified by <c>[STA-10]</c>.</summary>
    public object Store => Entry.Instance;

    /// <summary>
    /// Gets the exact reusable <see cref="StateStoreDefinition{TStore}"/> supplied to the registry.
    /// A plugin may cast to its known typed definition without reflection. Specified by
    /// <c>[STA-10]</c>.
    /// </summary>
    public object Definition => Entry.Definition;

    /// <summary>Gets the definition's diagnostic identifier. Specified by <c>[STA-10]</c>.</summary>
    public string Identifier { get; }

    /// <summary>Gets the definition's ordinal registry key. Specified by <c>[STA-10]</c>.</summary>
    public string Key => Entry.Key;

    /// <summary>
    /// Gets the same setup context supplied to the store activator. Specified by <c>[STA-10]</c>.
    /// </summary>
    public IStateContext StateContext { get; }

    /// <summary>
    /// Gets the store scope beneath the registry's detached root. Plugin subscriptions made during
    /// <see cref="IStateStorePlugin.Apply"/> attach here and survive component unmount. Specified
    /// by <c>[STA-3]</c> and <c>[STA-10]</c>.
    /// </summary>
    public IReactiveEffectScope Scope => StateContext.Scope;

    /// <summary>
    /// Gets the optional externally owned services supplied to the registry. Specified by
    /// <c>[STA-10]</c>.
    /// </summary>
    public IServiceProvider? Services => StateContext.Services;

    /// <summary>
    /// Tests the store against an explicitly named reference type without member discovery.
    /// Specified by <c>[STA-10]</c>.
    /// </summary>
    /// <typeparam name="TStore">The reference type expected by the plugin.</typeparam>
    /// <param name="store">The typed store on success; otherwise <see langword="null"/>.</param>
    /// <returns>Whether this store is assignable to the requested type.</returns>
    public bool TryGetStore<TStore>([NotNullWhen(true)] out TStore? store)
        where TStore : class
    {
        store = Store as TStore;
        return store is not null;
    }

    /// <summary>
    /// Attaches one extension under its exact declared type. Duplicate type keys are rejected;
    /// disposable extension instances are disposed once by reference when the store scope stops.
    /// Specified by <c>[STA-10]</c>.
    /// </summary>
    /// <typeparam name="TExtension">The statically selected extension key and value type.</typeparam>
    /// <param name="extension">The non-null extension owned by the store lifetime.</param>
    /// <exception cref="InvalidOperationException">This exact type key already has an extension.</exception>
    /// <exception cref="ObjectDisposedException">The store scope has stopped.</exception>
    public void SetExtension<TExtension>(TExtension extension)
        where TExtension : notnull
        => Entry.SetExtension(extension);

    internal StateStoreEntry Entry { get; }

    internal StateStorePersistenceDescriptor? Persistence { get; }
}
