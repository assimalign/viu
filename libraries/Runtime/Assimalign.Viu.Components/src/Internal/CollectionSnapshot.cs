using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Assimalign.Viu.Components;

internal static class CollectionSnapshot
{
    internal static IReadOnlyList<T> Copy<T>(IEnumerable<T>? source) =>
        source is null
            ? Array.Empty<T>()
            : new List<T>(source).AsReadOnly();

    internal static IReadOnlyList<T>? CopyNullable<T>(IEnumerable<T>? source) =>
        source is null
            ? null
            : new List<T>(source).AsReadOnly();

    internal static IReadOnlyList<T> CopyNonNull<T>(
        IEnumerable<T>? source,
        string parameterName)
        where T : class
    {
        if (source is null)
        {
            return Array.Empty<T>();
        }

        List<T> snapshot = new(source);
        for (int index = 0; index < snapshot.Count; index++)
        {
            if (snapshot[index] is null)
            {
                throw new ArgumentException(
                    "Collection entries cannot be null.",
                    parameterName);
            }
        }

        return snapshot.AsReadOnly();
    }

    internal static IReadOnlyList<T>? CopyNullableNonNull<T>(
        IEnumerable<T>? source,
        string parameterName)
        where T : class =>
        source is null ? null : CopyNonNull(source, parameterName);

    internal static IReadOnlyDictionary<string, TValue> CopyDictionary<TValue>(
        IReadOnlyDictionary<string, TValue>? source)
    {
        if (source is null || source.Count == 0)
        {
            return EmptyDictionary<TValue>.Value;
        }

        Dictionary<string, TValue> snapshot = new(source.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, TValue> entry in source)
        {
            ArgumentException.ThrowIfNullOrEmpty(entry.Key);
            snapshot.Add(entry.Key, entry.Value);
        }

        return new ReadOnlyDictionary<string, TValue>(snapshot);
    }

    internal static IReadOnlyDictionary<string, TValue> CopyNonNullDictionary<TValue>(
        IReadOnlyDictionary<string, TValue>? source,
        string parameterName)
        where TValue : class
    {
        IReadOnlyDictionary<string, TValue> snapshot = CopyDictionary(source);
        foreach (KeyValuePair<string, TValue> entry in snapshot)
        {
            if (entry.Value is null)
            {
                throw new ArgumentException(
                    "Dictionary values cannot be null.",
                    parameterName);
            }
        }

        return snapshot;
    }

    // [V01.01.11.07] The empty dictionary is immutable and holds no caller-owned collection.
    private static class EmptyDictionary<TValue>
    {
        internal static readonly IReadOnlyDictionary<string, TValue> Value =
            new ReadOnlyDictionary<string, TValue>(new Dictionary<string, TValue>(StringComparer.Ordinal));
    }
}
