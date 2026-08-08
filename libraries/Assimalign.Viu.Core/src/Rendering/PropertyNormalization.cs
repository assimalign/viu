using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Assimalign.Viu;

/// <summary>
/// Normalizes and merges compiler-generated property sources without reflection or dynamic code.
/// </summary>
/// <remarks>
/// Property sources are string-keyed dictionaries or key-value enumerables. Results are immutable
/// snapshots: class and style values normalize structurally, compatible event delegates combine in
/// source order, and every other repeated name uses the last value. This is the tier-one generated
/// code ABI specified by <c>[SFC-CG-2]</c>; merge ordering follows <c>[CMP-17]</c>.
/// </remarks>
public static class PropertyNormalization
{
    /// <summary>Merges generated property sources into one immutable ordinal snapshot.</summary>
    /// <param name="sources">
    /// The ordered sources. Null and unsupported entries contribute no properties.
    /// </param>
    /// <returns>
    /// A read-only dictionary whose class and style values are combined, compatible event
    /// delegates run in source order, and other duplicate names take the last value.
    /// </returns>
    public static IReadOnlyDictionary<string, object?> Merge(params object?[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        Dictionary<string, object?> merged = new(StringComparer.Ordinal);
        foreach (object? source in sources)
        {
            foreach (KeyValuePair<string, object?> property in ReadProperties(source))
            {
                if (string.Equals(property.Key, "class", StringComparison.Ordinal)
                    && merged.TryGetValue(property.Key, out object? existingClass))
                {
                    merged[property.Key] = StyleAndClassNormalization.NormalizeClass(
                        new object?[] { existingClass, property.Value });
                }
                else if (string.Equals(property.Key, "style", StringComparison.Ordinal)
                    && merged.TryGetValue(property.Key, out object? existingStyle))
                {
                    merged[property.Key] = StyleAndClassNormalization.NormalizeStyle(
                        new object?[] { existingStyle, property.Value });
                }
                else if (IsEventListenerName(property.Key)
                    && merged.TryGetValue(property.Key, out object? existingHandler)
                    && existingHandler is Delegate existingDelegate
                    && property.Value is Delegate incomingDelegate
                    && existingDelegate.GetType() == incomingDelegate.GetType()
                    && !ReferenceEquals(existingDelegate, incomingDelegate))
                {
                    merged[property.Key] = Delegate.Combine(existingDelegate, incomingDelegate);
                }
                else
                {
                    merged[property.Key] = property.Value;
                }
            }
        }

        return new ReadOnlyDictionary<string, object?>(merged);
    }

    /// <summary>Normalizes class and style entries in one generated property source.</summary>
    /// <param name="properties">
    /// A string-keyed dictionary, a key-value enumerable, or any other value.
    /// </param>
    /// <returns>
    /// An immutable normalized snapshot. Null and unreadable values produce an empty snapshot
    /// without reflection-based member discovery.
    /// </returns>
    public static IReadOnlyDictionary<string, object?> Normalize(object? properties)
    {
        Dictionary<string, object?> normalized = CopyProperties(properties);
        if (normalized.TryGetValue("class", out object? cssClass))
        {
            normalized["class"] = StyleAndClassNormalization.NormalizeClass(cssClass);
        }

        if (normalized.TryGetValue("style", out object? style))
        {
            normalized["style"] = StyleAndClassNormalization.NormalizeStyle(style);
        }

        return new ReadOnlyDictionary<string, object?>(normalized);
    }

    private static Dictionary<string, object?> CopyProperties(object? source)
    {
        Dictionary<string, object?> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> property in ReadProperties(source))
        {
            result[property.Key] = property.Value;
        }

        return result;
    }

    private static IEnumerable<KeyValuePair<string, object?>> ReadProperties(object? source)
    {
        switch (source)
        {
            case null:
                yield break;
            case IReadOnlyDictionary<string, object?> dictionary:
                foreach (KeyValuePair<string, object?> property in dictionary)
                {
                    yield return property;
                }

                yield break;
            case IEnumerable<KeyValuePair<string, object?>> values:
                foreach (KeyValuePair<string, object?> property in values)
                {
                    yield return property;
                }

                yield break;
        }
    }

    private static bool IsEventListenerName(string name) =>
        name.Length > 2
            && name[0] == 'o'
            && name[1] == 'n'
            && char.IsAsciiLetterUpper(name[2]);
}
