using System;
using System.Collections.Generic;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Receives direct compiler-generated server catalog delegates. Registration and snapshotting are
/// synchronized; each target registry controls its own lifetime. Specified by <c>[CMP-36]</c>.
/// </summary>
public static class GeneratedServerRenderCatalogs
{
    private static readonly object Synchronization = new();
    private static readonly SortedDictionary<string, Action<ServerRenderRegistry>> Catalogs = new(StringComparer.Ordinal);

    /// <summary>Adds or replaces a catalog for future registration, preserving ordinal identity order.</summary>
    /// <param name="identity">The stable assembly identity.</param>
    /// <param name="register">The direct generated registration delegate.</param>
    public static void Register(string identity, Action<ServerRenderRegistry> register)
    {
        ArgumentException.ThrowIfNullOrEmpty(identity);
        ArgumentNullException.ThrowIfNull(register);
        lock (Synchronization)
        {
            Catalogs[identity] = register;
        }
    }

    internal static void Apply(ServerRenderRegistry registry)
    {
        KeyValuePair<string, Action<ServerRenderRegistry>>[] snapshot;
        lock (Synchronization)
        {
            snapshot = new KeyValuePair<string, Action<ServerRenderRegistry>>[Catalogs.Count];
            ((ICollection<KeyValuePair<string, Action<ServerRenderRegistry>>>)Catalogs).CopyTo(snapshot, 0);
        }
        foreach (KeyValuePair<string, Action<ServerRenderRegistry>> catalog in snapshot)
        {
            registry.RegisterGeneratedCatalog(catalog.Key, catalog.Value);
        }
    }
}
