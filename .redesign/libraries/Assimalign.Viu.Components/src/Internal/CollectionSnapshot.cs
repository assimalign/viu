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

    internal static IReadOnlyDictionary<string, TValue> CopyDictionary<TValue>(
        IReadOnlyDictionary<string, TValue>? source)
    {
        if (source is null || source.Count == 0)
        {
            return new ReadOnlyDictionary<string, TValue>(
                new Dictionary<string, TValue>(StringComparer.Ordinal));
        }

        return new ReadOnlyDictionary<string, TValue>(
            new Dictionary<string, TValue>(source, StringComparer.Ordinal));
    }
}
