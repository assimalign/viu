using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.DevTools;

internal sealed class SnapshotValueEncoder
{
    private readonly int _maximumEntries;

    internal SnapshotValueEncoder(int maximumEntries) =>
        _maximumEntries = maximumEntries;

    internal List<DevToolsNamedValuePayload> EncodeDictionary(
        IReadOnlyDictionary<string, object?> values,
        int depth)
    {
        List<DevToolsNamedValuePayload> encoded = [];
        HashSet<object> ancestors = new(ReferenceEqualityComparer.Instance);
        int count = 0;
        try
        {
            foreach (KeyValuePair<string, object?> entry in values)
            {
                if (count++ == _maximumEntries)
                {
                    encoded.Add(new DevToolsNamedValuePayload(
                        "...",
                        Placeholder("collection-limit", typeof(object), [])));
                    break;
                }

                List<string> path = [entry.Key];
                encoded.Add(new DevToolsNamedValuePayload(
                    entry.Key,
                    Encode(entry.Value, depth, path, ancestors)));
            }
        }
        catch
        {
            encoded.Add(new DevToolsNamedValuePayload(
                "...",
                Placeholder("unavailable", values.GetType(), [])));
        }

        return encoded;
    }

    internal DevToolsValuePayload EncodeValue(
        object? value,
        int depth,
        IReadOnlyList<string> path)
    {
        HashSet<object> ancestors = new(ReferenceEqualityComparer.Instance);
        return Encode(value, depth, new List<string>(path), ancestors);
    }

    internal static bool TryResolvePath(
        object? root,
        IReadOnlyList<string> path,
        out object? value)
    {
        value = root;
        for (int index = 0; index < path.Count; index++)
        {
            try
            {
                if (value is IReactiveReference reactiveReference
                    && string.Equals(path[index], "value", StringComparison.Ordinal))
                {
                    value = reactiveReference.Value;
                    continue;
                }

                if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
                    && readOnlyDictionary.TryGetValue(path[index], out object? dictionaryValue))
                {
                    value = dictionaryValue;
                    continue;
                }

                if (value is IDictionary dictionary)
                {
                    bool found = false;
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        if (!string.Equals(
                                SafeDisplay(entry.Key),
                                path[index],
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        value = entry.Value;
                        found = true;
                        break;
                    }

                    if (found)
                    {
                        continue;
                    }
                }

                if (value is IList list
                    && int.TryParse(path[index], NumberStyles.None, CultureInfo.InvariantCulture, out int itemIndex)
                    && itemIndex >= 0
                    && itemIndex < list.Count)
                {
                    value = list[itemIndex];
                    continue;
                }
            }
            catch
            {
                value = new UnavailableInspectionValue("getter-threw");
                return true;
            }

            value = null;
            return false;
        }

        return true;
    }

    private DevToolsValuePayload Encode(
        object? value,
        int depth,
        List<string> path,
        HashSet<object> ancestors)
    {
        if (value is null)
        {
            return new DevToolsValuePayload("null", "null", null, false, path, []);
        }

        Type type = value.GetType();
        string typeName = type.FullName ?? type.Name;
        if (value is UnavailableInspectionValue unavailable)
        {
            return Placeholder(unavailable.Reason, type, path);
        }

        if (TryFormatScalar(value, out string? displayValue))
        {
            return new DevToolsValuePayload(
                "scalar",
                typeName,
                displayValue,
                false,
                path,
                []);
        }

        bool tracksCycle = !type.IsValueType;
        if (tracksCycle && !ancestors.Add(value))
        {
            return Placeholder("cycle", type, path);
        }

        try
        {
            if (value is IReactiveReference reactiveReference)
            {
                if (depth == 0)
                {
                    return Deferred("reference", typeName, path);
                }

                object? current;
                try
                {
                    current = reactiveReference.Value;
                }
                catch
                {
                    return Placeholder("unavailable", type, path);
                }

                List<string> childPath = [.. path, "value"];
                return new DevToolsValuePayload(
                    "reference",
                    typeName,
                    null,
                    true,
                    path,
                    [new DevToolsNamedValuePayload(
                        "value",
                        Encode(current, depth - 1, childPath, ancestors))]);
            }

            if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
            {
                if (depth == 0)
                {
                    return Deferred("object", typeName, path);
                }

                return EncodeReadOnlyDictionary(
                    readOnlyDictionary,
                    depth,
                    path,
                    ancestors,
                    typeName);
            }

            if (value is IDictionary dictionary)
            {
                if (depth == 0)
                {
                    return Deferred("object", typeName, path);
                }

                return EncodeDictionaryObject(dictionary, depth, path, ancestors, typeName);
            }

            if (value is IList list)
            {
                if (depth == 0)
                {
                    return Deferred("array", typeName, path);
                }

                return EncodeList(list, depth, path, ancestors, typeName);
            }

            return Placeholder("non-serializable", type, path);
        }
        catch
        {
            return Placeholder("unavailable", type, path);
        }
        finally
        {
            if (tracksCycle)
            {
                ancestors.Remove(value);
            }
        }
    }

    private DevToolsValuePayload EncodeReadOnlyDictionary(
        IReadOnlyDictionary<string, object?> dictionary,
        int depth,
        List<string> path,
        HashSet<object> ancestors,
        string typeName)
    {
        List<DevToolsNamedValuePayload> children = [];
        int count = 0;
        foreach (KeyValuePair<string, object?> entry in dictionary)
        {
            if (count++ == _maximumEntries)
            {
                children.Add(new DevToolsNamedValuePayload(
                    "...",
                    Placeholder("collection-limit", typeof(object), path)));
                break;
            }

            List<string> childPath = [.. path, entry.Key];
            children.Add(new DevToolsNamedValuePayload(
                entry.Key,
                Encode(entry.Value, depth - 1, childPath, ancestors)));
        }

        return new DevToolsValuePayload("object", typeName, null, true, path, children);
    }

    private DevToolsValuePayload EncodeDictionaryObject(
        IDictionary dictionary,
        int depth,
        List<string> path,
        HashSet<object> ancestors,
        string typeName)
    {
        List<DevToolsNamedValuePayload> children = [];
        int count = 0;
        foreach (DictionaryEntry entry in dictionary)
        {
            if (count++ == _maximumEntries)
            {
                children.Add(new DevToolsNamedValuePayload(
                    "...",
                    Placeholder("collection-limit", typeof(object), path)));
                break;
            }

            string name = entry.Key as string ?? SafeDisplay(entry.Key);
            List<string> childPath = [.. path, name];
            children.Add(new DevToolsNamedValuePayload(
                name,
                Encode(entry.Value, depth - 1, childPath, ancestors)));
        }

        return new DevToolsValuePayload("object", typeName, null, true, path, children);
    }

    private DevToolsValuePayload EncodeList(
        IList list,
        int depth,
        List<string> path,
        HashSet<object> ancestors,
        string typeName)
    {
        List<DevToolsNamedValuePayload> children = [];
        int count = Math.Min(list.Count, _maximumEntries);
        for (int index = 0; index < count; index++)
        {
            string name = index.ToString(CultureInfo.InvariantCulture);
            List<string> childPath = [.. path, name];
            children.Add(new DevToolsNamedValuePayload(
                name,
                Encode(list[index], depth - 1, childPath, ancestors)));
        }

        if (list.Count > count)
        {
            children.Add(new DevToolsNamedValuePayload(
                "...",
                Placeholder("collection-limit", typeof(object), path)));
        }

        return new DevToolsValuePayload("array", typeName, null, true, path, children);
    }

    private static DevToolsValuePayload Deferred(
        string kind,
        string typeName,
        List<string> path) => new(
            kind,
            typeName,
            null,
            true,
            path,
            []);

    private static DevToolsValuePayload Placeholder(
        string reason,
        Type type,
        IReadOnlyList<string> path) => new(
            "placeholder",
            type.FullName ?? type.Name,
            reason,
            false,
            new List<string>(path),
            []);

    internal static string SafeDisplay(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        return TryFormatScalar(value, out string? display)
            ? display ?? "null"
            : $"<{value.GetType().FullName ?? value.GetType().Name}>";
    }

    private static bool TryFormatScalar(object value, out string? display)
    {
        switch (value)
        {
            case string text:
                display = text;
                return true;
            case bool condition:
                display = condition ? "true" : "false";
                return true;
            case char character:
                display = character.ToString();
                return true;
            case byte number:
                display = number.ToString(CultureInfo.InvariantCulture);
                return true;
            case sbyte number:
                display = number.ToString(CultureInfo.InvariantCulture);
                return true;
            case short number:
                display = number.ToString(CultureInfo.InvariantCulture);
                return true;
            case ushort number:
                display = number.ToString(CultureInfo.InvariantCulture);
                return true;
            case int number:
                display = number.ToString(CultureInfo.InvariantCulture);
                return true;
            case uint number:
                display = number.ToString(CultureInfo.InvariantCulture);
                return true;
            case long number:
                display = number.ToString(CultureInfo.InvariantCulture);
                return true;
            case ulong number:
                display = number.ToString(CultureInfo.InvariantCulture);
                return true;
            case float number:
                display = number.ToString("R", CultureInfo.InvariantCulture);
                return true;
            case double number:
                display = number.ToString("R", CultureInfo.InvariantCulture);
                return true;
            case decimal number:
                display = number.ToString(CultureInfo.InvariantCulture);
                return true;
            case DateTime dateTime:
                display = dateTime.ToString("O", CultureInfo.InvariantCulture);
                return true;
            case DateTimeOffset dateTimeOffset:
                display = dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
                return true;
            case TimeSpan timeSpan:
                display = timeSpan.ToString("c", CultureInfo.InvariantCulture);
                return true;
            case Guid identifier:
                display = identifier.ToString("D");
                return true;
            case Enum enumeration:
                display = enumeration.ToString();
                return true;
            default:
                display = null;
                return false;
        }
    }
}
