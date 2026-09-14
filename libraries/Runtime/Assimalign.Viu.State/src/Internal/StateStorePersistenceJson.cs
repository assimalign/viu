using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Assimalign.Viu.State;

internal static class StateStorePersistenceJson
{
    internal static JsonObject ReadObject(JsonElement state)
    {
        ValidateMembers(state);
        return JsonNode.Parse(state.GetRawText()) as JsonObject
            ?? throw new JsonException("Persisted state must be a JSON object.");
    }

    internal static JsonObject Select(JsonObject state, StateStorePersistenceDescriptor descriptor)
        => SelectObject(state, descriptor, string.Empty, descriptor.IncludePaths.Count == 0);

    internal static JsonObject Merge(JsonObject defaults, JsonObject selected)
    {
        JsonObject merged = (JsonObject)defaults.DeepClone();
        foreach (KeyValuePair<string, JsonNode?> member in selected)
        {
            if (!defaults.TryGetPropertyValue(member.Key, out JsonNode? defaultValue))
            {
                throw new JsonException($"Persisted state contains unknown member \"{member.Key}\".");
            }

            merged[member.Key] = MergeValue(defaultValue, member.Value);
        }

        return merged;
    }

    internal static JsonElement ToElement(JsonObject state)
    {
        using JsonDocument document = JsonDocument.Parse(state.ToJsonString());
        return document.RootElement.Clone();
    }

    internal static string CreatePayload(string identifier, string selectedJson)
    {
        using JsonDocument document = JsonDocument.Parse(selectedJson);
        return StateStorePayload.Create(new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [identifier] = document.RootElement,
        }).Json;
    }

    private static JsonObject SelectObject(
        JsonObject state,
        StateStorePersistenceDescriptor descriptor,
        string parentPath,
        bool includeAll)
    {
        JsonObject selected = new();
        foreach (KeyValuePair<string, JsonNode?> member in state)
        {
            string path = parentPath.Length == 0 ? member.Key : parentPath + "." + member.Key;
            if (Contains(descriptor.ExcludePaths, path))
            {
                continue;
            }

            bool includeMember = includeAll || Contains(descriptor.IncludePaths, path);
            if (!includeMember && !HasDescendant(descriptor.IncludePaths, path))
            {
                continue;
            }

            if (member.Value is JsonObject child)
            {
                selected[member.Key] = SelectObject(child, descriptor, path, includeMember);
            }
            else if (includeMember)
            {
                selected[member.Key] = member.Value?.DeepClone();
            }
        }

        return selected;
    }

    private static bool Contains(IReadOnlyList<string> paths, string path)
    {
        foreach (string candidate in paths)
        {
            if (string.Equals(candidate, path, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDescendant(IReadOnlyList<string> paths, string path)
    {
        string prefix = path + ".";
        foreach (string candidate in paths)
        {
            if (candidate.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static JsonNode? MergeValue(JsonNode? defaultValue, JsonNode? persistedValue)
    {
        // Nullability and array element contracts belong to the registered typed serializer.
        if (defaultValue is null || persistedValue is null)
        {
            return persistedValue?.DeepClone();
        }

        if (defaultValue is JsonObject defaultObject && persistedValue is JsonObject persistedObject)
        {
            return Merge(defaultObject, persistedObject);
        }

        JsonValueKind defaultKind = defaultValue.GetValueKind();
        JsonValueKind persistedKind = persistedValue.GetValueKind();
        bool bothBoolean = defaultKind is JsonValueKind.True or JsonValueKind.False
            && persistedKind is JsonValueKind.True or JsonValueKind.False;
        if (defaultKind != persistedKind && !bothBoolean)
        {
            throw new JsonException("Persisted state has an incompatible JSON member kind.");
        }

        return persistedValue.DeepClone();
    }

    private static void ValidateMembers(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty member in element.EnumerateObject())
            {
                if (!names.Add(member.Name))
                {
                    throw new JsonException($"Persisted state contains duplicate member \"{member.Name}\".");
                }

                ValidateMembers(member.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                ValidateMembers(item);
            }
        }
    }
}
