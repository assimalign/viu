using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Store;

/// <summary>
/// A registry's record for one resolved store — its <see cref="Instance"/>, the
/// <see cref="EffectScope"/> that owns the computeds and watchers created in setup, and the
/// <see cref="Definition"/> that created it (held by reference so the registry can tell an id reused
/// by a different definition from the same definition resolved again). The C# analogue of the entry
/// Pinia keeps in <c>pinia._s</c> alongside the store's <c>scope</c>. Internal.
/// </summary>
internal sealed class StoreEntry
{
    public StoreEntry(object definition, object instance, EffectScope scope)
    {
        Definition = definition;
        Instance = instance;
        Scope = scope;
    }

    /// <summary>The definition that owns the id (reference identity drives duplicate-id detection).</summary>
    public object Definition { get; }

    /// <summary>The cached store instance.</summary>
    public object Instance { get; }

    /// <summary>The store's own effect scope (a child of the registry's root scope).</summary>
    public EffectScope Scope { get; }
}
