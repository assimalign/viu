using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Receives compiler-generated catalog delegates and replays them into application-owned factories.
/// No assemblies are inspected at runtime. Publication and snapshots are synchronized; each factory
/// retains its own single-threaded registration lifetime.
/// Specified by <c>[CMP-36]</c>.
/// </summary>
public static class GeneratedComponentCatalogs
{
    private static readonly SortedDictionary<string, Action<ComponentFactory>> Catalogs = new(StringComparer.Ordinal);
    private static readonly object Synchronization = new();

    /// <summary>
    /// Adds or replaces a compiler-known catalog. Replacement preserves ordinal ordering and allows
    /// subsequent factories to observe updated generated code; existing factories retain registrations.
    /// </summary>
    /// <param name="identity">The catalog's stable assembly identity.</param>
    /// <param name="register">The direct generated registration delegate.</param>
    public static void Register(string identity, Action<ComponentFactory> register)
    {
        ArgumentException.ThrowIfNullOrEmpty(identity);
        ArgumentNullException.ThrowIfNull(register);
        lock (Synchronization)
        {
            Catalogs[identity] = register;
        }
    }

    internal static void Apply(ComponentFactory factory)
    {
        KeyValuePair<string, Action<ComponentFactory>>[] snapshot;
        lock (Synchronization)
        {
            snapshot = new KeyValuePair<string, Action<ComponentFactory>>[Catalogs.Count];
            ((ICollection<KeyValuePair<string, Action<ComponentFactory>>>)Catalogs).CopyTo(snapshot, 0);
        }
        foreach (KeyValuePair<string, Action<ComponentFactory>> catalog in snapshot)
        {
            factory.RegisterGeneratedCatalog(catalog.Key, catalog.Value);
        }
    }
}
