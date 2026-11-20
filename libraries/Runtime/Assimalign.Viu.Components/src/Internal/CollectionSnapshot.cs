using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Builds the immutable collection snapshots that virtual nodes, plans, contracts, and invocations
/// hold. Every snapshot is a single copy: a caller's collection is copied once into a
/// <see cref="SnapshotList{T}"/> or <see cref="SnapshotDictionary{TValue}"/>, an existing snapshot
/// is adopted without copying, and an empty result is a shared empty instance. Null-entry validation runs once and is remembered on the
/// snapshot, so a plan built from a closed render block never rescans it. Specified by
/// <c>[CMP-2]</c>; the allocation shape is pinned by <c>[V01.01.11.07]</c>.
/// </summary>
internal static class CollectionSnapshot
{
    internal static IReadOnlyList<T> Copy<T>(IEnumerable<T>? source)
    {
        if (source is null)
        {
            return Array.Empty<T>();
        }

        if (source is SnapshotList<T> snapshot)
        {
            return snapshot;
        }

        T[] items = ToArray(source);
        return items.Length == 0
            ? Array.Empty<T>()
            : new SnapshotList<T>(items, isNullFree: false);
    }

    internal static IReadOnlyList<T>? CopyNullable<T>(IEnumerable<T>? source) =>
        source is null ? null : Copy(source);

    internal static IReadOnlyList<T> CopyNonNull<T>(
        IEnumerable<T>? source,
        string parameterName)
        where T : class
    {
        if (source is null)
        {
            return Array.Empty<T>();
        }

        if (source is SnapshotList<T> snapshot)
        {
            if (snapshot.IsNullFree)
            {
                return snapshot;
            }

            ThrowIfAnyNull(snapshot.Items, parameterName);
            return new SnapshotList<T>(snapshot.Items, isNullFree: true);
        }

        T[] items = ToArray(source);
        if (items.Length == 0)
        {
            return Array.Empty<T>();
        }

        ThrowIfAnyNull(items, parameterName);
        return new SnapshotList<T>(items, isNullFree: true);
    }

    internal static IReadOnlyList<T>? CopyNullableNonNull<T>(
        IEnumerable<T>? source,
        string parameterName)
        where T : class =>
        source is null ? null : CopyNonNull(source, parameterName);

    private static T[] ToArray<T>(IEnumerable<T> source)
    {
        if (source is ICollection<T> collection)
        {
            int count = collection.Count;
            if (count == 0)
            {
                return Array.Empty<T>();
            }

            T[] items = new T[count];
            collection.CopyTo(items, 0);
            return items;
        }

        return new List<T>(source).ToArray();
    }

    private static void ThrowIfAnyNull<T>(T[] items, string parameterName)
        where T : class
    {
        for (int index = 0; index < items.Length; index++)
        {
            if (items[index] is null)
            {
                throw new ArgumentException(
                    "Collection entries cannot be null.",
                    parameterName);
            }
        }
    }

    internal static IReadOnlyDictionary<string, TValue> CopyDictionary<TValue>(
        IReadOnlyDictionary<string, TValue>? source) =>
        CopyDictionarySnapshot(source);

    internal static IReadOnlyDictionary<string, TValue> CopyNonNullDictionary<TValue>(
        IReadOnlyDictionary<string, TValue>? source,
        string parameterName)
        where TValue : class
    {
        SnapshotDictionary<TValue> snapshot = CopyDictionarySnapshot(source);
        if (snapshot.IsNullFree)
        {
            return snapshot;
        }

        foreach (TValue value in snapshot.Entries.Values)
        {
            if (value is null)
            {
                throw new ArgumentException(
                    "Dictionary values cannot be null.",
                    parameterName);
            }
        }

        return new SnapshotDictionary<TValue>(snapshot.Entries, isNullFree: true);
    }

    private static SnapshotDictionary<TValue> CopyDictionarySnapshot<TValue>(
        IReadOnlyDictionary<string, TValue>? source)
    {
        if (source is null || source.Count == 0)
        {
            return SnapshotDictionary<TValue>.Empty;
        }

        if (source is SnapshotDictionary<TValue> snapshot)
        {
            return snapshot;
        }

        Dictionary<string, TValue> entries = new(source.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, TValue> entry in source)
        {
            ArgumentException.ThrowIfNullOrEmpty(entry.Key);
            entries.Add(entry.Key, entry.Value);
        }

        return new SnapshotDictionary<TValue>(entries, isNullFree: false);
    }
}
