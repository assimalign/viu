using System;
using System.Collections;
using System.Collections.Generic;

namespace Assimalign.Viu.Browser;

/// <summary>Provides Browser form-control value matching after DOM string coercion.</summary>
/// <remarks>
/// Dates compare by instant, sequences element by element, maps by ordinal key, and remaining
/// scalars by invariant display text. The implementation performs no runtime type discovery and
/// is safe for trimmed WASM applications. Specified by <c>[SFC-CG-7]</c>.
/// </remarks>
internal static class LooseEquality
{
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

        bool leftIsDate = TryGetInstant(left, out long leftInstant);
        bool rightIsDate = TryGetInstant(right, out long rightInstant);
        if (leftIsDate || rightIsDate)
        {
            return leftIsDate && rightIsDate && leftInstant == rightInstant;
        }

        bool leftIsMap = IsMap(left);
        bool rightIsMap = IsMap(right);
        if (leftIsMap || rightIsMap)
        {
            return leftIsMap && rightIsMap && MapsLooselyEqual(left, right);
        }

        bool leftIsEnumerable = left is IEnumerable and not string and not IDictionary;
        bool rightIsEnumerable = right is IEnumerable and not string and not IDictionary;
        if (leftIsEnumerable || rightIsEnumerable)
        {
            return leftIsEnumerable
                && rightIsEnumerable
                && SequencesLooselyEqual((IEnumerable)left, (IEnumerable)right);
        }

        return string.Equals(
            DisplayStringFormatter.FormatScalar(left),
            DisplayStringFormatter.FormatScalar(right),
            StringComparison.Ordinal);
    }

    public static int LooseIndexOf(IEnumerable values, object? value)
    {
        ArgumentNullException.ThrowIfNull(values);
        int index = 0;
        foreach (object? entry in values)
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

    private static bool IsMap(object value) =>
        value is IDictionary || value is IReadOnlyDictionary<string, object?>;

    private static bool MapsLooselyEqual(object left, object right)
    {
        Dictionary<string, object?> leftPairs = EnumerateMap(left);
        Dictionary<string, object?> rightPairs = EnumerateMap(right);
        if (leftPairs.Count != rightPairs.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, object?> pair in leftPairs)
        {
            if (!rightPairs.TryGetValue(pair.Key, out object? rightValue)
                || !LooseEqual(pair.Value, rightValue))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, object?> EnumerateMap(object value)
    {
        Dictionary<string, object?> pairs = new(StringComparer.Ordinal);
        if (value is IReadOnlyDictionary<string, object?> readOnlyMap)
        {
            foreach (KeyValuePair<string, object?> pair in readOnlyMap)
            {
                pairs[pair.Key] = pair.Value;
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
        IEnumerator leftEnumerator = left.GetEnumerator();
        IEnumerator rightEnumerator = right.GetEnumerator();
        try
        {
            while (true)
            {
                bool leftHasNext = leftEnumerator.MoveNext();
                bool rightHasNext = rightEnumerator.MoveNext();
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
