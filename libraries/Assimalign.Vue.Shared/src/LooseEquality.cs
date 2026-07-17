using System;
using System.Collections;
using System.Collections.Generic;

namespace Assimalign.Vue.Shared;

/// <summary>
/// Vue's loose equality — the C# port of <c>looseEqual</c>/<c>looseIndexOf</c> from
/// <c>@vue/shared</c> (<c>packages/shared/src/looseEqual.ts</c>). Checkbox and select
/// <c>v-model</c> value matching depends on these semantics: dates compare by instant,
/// enumerables element-wise, dictionaries key-wise, and everything else falls back to
/// invariant display-string comparison (JavaScript's <c>String(a) === String(b)</c> coercion,
/// so <c>1</c> loosely equals <c>"1"</c>).
/// </summary>
public static class LooseEquality
{
    /// <summary>Whether <paramref name="left"/> and <paramref name="right"/> are loosely equal (upstream: <c>looseEqual</c>).</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    public static bool LooseEqual(object? left, object? right)
    {
        if (Equals(left, right))
        {
            return true;
        }
        if (left is null || right is null)
        {
            return false;
        }
        var leftIsDate = TryGetInstant(left, out var leftInstant);
        var rightIsDate = TryGetInstant(right, out var rightInstant);
        if (leftIsDate || rightIsDate)
        {
            return leftIsDate && rightIsDate && leftInstant == rightInstant;
        }
        var leftIsEnumerable = left is IEnumerable and not string and not IDictionary;
        var rightIsEnumerable = right is IEnumerable and not string and not IDictionary;
        var leftIsMap = IsMap(left);
        var rightIsMap = IsMap(right);
        if (leftIsMap || rightIsMap)
        {
            return leftIsMap && rightIsMap && MapsLooselyEqual(left, right);
        }
        if (leftIsEnumerable || rightIsEnumerable)
        {
            return leftIsEnumerable && rightIsEnumerable
                && SequencesLooselyEqual((IEnumerable)left, (IEnumerable)right);
        }
        // JavaScript's final fallback: String(a) === String(b).
        return string.Equals(
            DisplayStringFormatter.FormatScalar(left),
            DisplayStringFormatter.FormatScalar(right),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The index of the first element loosely equal to <paramref name="value"/>, or -1
    /// (upstream: <c>looseIndexOf</c>) — how <c>v-model</c> matches a checkbox/select value
    /// against a bound collection.
    /// </summary>
    /// <param name="values">The collection to search.</param>
    /// <param name="value">The value to match.</param>
    public static int LooseIndexOf(IEnumerable values, object? value)
    {
        ArgumentNullException.ThrowIfNull(values);
        var index = 0;
        foreach (var entry in values)
        {
            if (LooseEqual(entry, value))
            {
                return index;
            }
            index++;
        }
        return -1;
    }

    private static bool TryGetInstant(object value, out long instant)
    {
        switch (value)
        {
            case DateTime dateTime:
                instant = dateTime.ToUniversalTime().Ticks;
                return true;
            case DateTimeOffset dateTimeOffset:
                instant = dateTimeOffset.UtcTicks;
                return true;
            default:
                instant = 0;
                return false;
        }
    }

    private static bool IsMap(object value)
        => value is IDictionary || value is IReadOnlyDictionary<string, object?>;

    private static bool MapsLooselyEqual(object left, object right)
    {
        var leftPairs = EnumerateMap(left);
        var rightPairs = EnumerateMap(right);
        if (leftPairs.Count != rightPairs.Count)
        {
            return false;
        }
        foreach (var (name, leftValue) in leftPairs)
        {
            if (!rightPairs.TryGetValue(name, out var rightValue) || !LooseEqual(leftValue, rightValue))
            {
                return false;
            }
        }
        return true;
    }

    private static Dictionary<string, object?> EnumerateMap(object value)
    {
        var pairs = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (value is IReadOnlyDictionary<string, object?> readOnlyMap)
        {
            foreach (var (name, entryValue) in readOnlyMap)
            {
                pairs[name] = entryValue;
            }
        }
        else if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                pairs[DisplayStringFormatter.FormatScalar(entry.Key)] = entry.Value;
            }
        }
        return pairs;
    }

    private static bool SequencesLooselyEqual(IEnumerable left, IEnumerable right)
    {
        var leftEnumerator = left.GetEnumerator();
        var rightEnumerator = right.GetEnumerator();
        try
        {
            while (true)
            {
                var leftHasNext = leftEnumerator.MoveNext();
                var rightHasNext = rightEnumerator.MoveNext();
                if (leftHasNext != rightHasNext)
                {
                    return false;
                }
                if (!leftHasNext)
                {
                    return true;
                }
                if (!LooseEqual(leftEnumerator.Current, rightEnumerator.Current))
                {
                    return false;
                }
            }
        }
        finally
        {
            (leftEnumerator as IDisposable)?.Dispose();
            (rightEnumerator as IDisposable)?.Dispose();
        }
    }
}
