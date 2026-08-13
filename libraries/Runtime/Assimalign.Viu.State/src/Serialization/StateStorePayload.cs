using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Assimalign.Viu.State;

/// <summary>
/// Carries a validated, immutable snapshot of registry-owned state using schema
/// <c>{"version":1,"stores":{"store-key":state}}</c>. Store keys compare ordinally and each
/// state value is produced only by its registered serializer. Specified by <c>[STA-9]</c> and
/// constrained by <c>[EXE-4]</c>.
/// </summary>
public sealed class StateStorePayload
{
    /// <summary>
    /// Gets the only schema version accepted by this release. Specified by
    /// <c>[STA-9]</c>.
    /// </summary>
    public const int CurrentVersion = 1;

    private readonly IReadOnlyDictionary<string, JsonElement> _stores;

    private StateStorePayload(
        string json,
        IReadOnlyDictionary<string, JsonElement> stores)
    {
        Json = json;
        _stores = stores;
        string[] keys = new string[stores.Count];
        int index = 0;
        foreach (string key in stores.Keys)
        {
            keys[index++] = key;
        }

        StoreKeys = new ReadOnlyCollection<string>(keys);
    }

    /// <summary>
    /// Gets this payload's fixed schema version. Specified by <c>[STA-9]</c>.
    /// </summary>
    public int Version => CurrentVersion;

    /// <summary>
    /// Gets the ordinal keys of the stores represented by the payload. Specified by
    /// <c>[STA-9]</c>.
    /// </summary>
    public IReadOnlyList<string> StoreKeys { get; }

    /// <summary>
    /// Gets the normalized JSON document. HTML-sensitive characters and Unicode line separators
    /// are escaped, so the value cannot terminate a JSON script island when embedded verbatim.
    /// </summary>
    /// <remarks>Specified by <c>[STA-9]</c> and constrained by <c>[EXE-4]</c>.</remarks>
    public string Json { get; }

    /// <summary>
    /// Parses and validates the versioned payload without reflection-backed deserialization.
    /// Unknown or duplicate schema members and duplicate or empty store keys are rejected.
    /// </summary>
    /// <remarks>Specified by <c>[STA-9]</c>.</remarks>
    /// <param name="json">The complete payload JSON.</param>
    /// <returns>A validated, normalized payload.</returns>
    /// <exception cref="JsonException">The input does not conform to the payload schema.</exception>
    public static StateStorePayload Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A Viu state payload must be a JSON object.");
        }

        int? version = null;
        Dictionary<string, JsonElement>? stores = null;
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.NameEquals("version"))
            {
                if (version is not null
                    || property.Value.ValueKind != JsonValueKind.Number
                    || !property.Value.TryGetInt32(out int parsedVersion))
                {
                    throw new JsonException(
                        "A Viu state payload must contain one integer version member.");
                }

                version = parsedVersion;
            }
            else if (property.NameEquals("stores"))
            {
                if (stores is not null || property.Value.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException(
                        "A Viu state payload must contain one object-valued stores member.");
                }

                stores = ReadStores(property.Value);
            }
            else
            {
                throw new JsonException(
                    $"Unknown Viu state payload member \"{property.Name}\".");
            }
        }

        if (version != CurrentVersion)
        {
            throw new JsonException(
                $"Unsupported Viu state payload version {version?.ToString() ?? "<missing>"}; "
                + $"expected {CurrentVersion}.");
        }

        if (stores is null)
        {
            throw new JsonException("A Viu state payload requires a stores member.");
        }

        return Create(stores);
    }

    internal static StateStorePayload Create(
        IReadOnlyDictionary<string, StateStoreEntry> entries)
    {
        Dictionary<string, JsonElement> stores = new(
            entries.Count,
            StringComparer.Ordinal);
        foreach (KeyValuePair<string, StateStoreEntry> entry in entries)
        {
            stores.Add(entry.Key, entry.Value.SerializeState());
        }

        return Create(stores);
    }

    internal bool TryGetState(string key, out JsonElement state) =>
        _stores.TryGetValue(key, out state);

    private static StateStorePayload Create(
        IReadOnlyDictionary<string, JsonElement> stores)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(
            buffer,
            new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.Default,
            }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WriteStartObject("stores");
            foreach (KeyValuePair<string, JsonElement> store in stores)
            {
                writer.WritePropertyName(store.Key);
                store.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        string json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        Dictionary<string, JsonElement> copies = new(
            stores.Count,
            StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonElement> store in stores)
        {
            copies.Add(store.Key, store.Value.Clone());
        }

        return new StateStorePayload(
            EscapeForJsonIsland(json),
            new ReadOnlyDictionary<string, JsonElement>(copies));
    }

    private static Dictionary<string, JsonElement> ReadStores(JsonElement storesElement)
    {
        Dictionary<string, JsonElement> stores = new(StringComparer.Ordinal);
        foreach (JsonProperty store in storesElement.EnumerateObject())
        {
            if (store.Name.Length == 0)
            {
                throw new JsonException("A Viu state payload cannot contain an empty store key.");
            }

            if (!stores.TryAdd(store.Name, store.Value.Clone()))
            {
                throw new JsonException(
                    $"A Viu state payload contains duplicate store key \"{store.Name}\".");
            }
        }

        return stores;
    }

    private static string EscapeForJsonIsland(string json)
    {
        StringBuilder? builder = null;
        for (int index = 0; index < json.Length; index++)
        {
            string? replacement = json[index] switch
            {
                '<' => "\\u003C",
                '>' => "\\u003E",
                '&' => "\\u0026",
                '\u2028' => "\\u2028",
                '\u2029' => "\\u2029",
                _ => null,
            };
            if (replacement is null)
            {
                builder?.Append(json[index]);
                continue;
            }

            if (builder is null)
            {
                builder = new StringBuilder(json.Length + 16);
                builder.Append(json, 0, index);
            }

            builder.Append(replacement);
        }

        return builder?.ToString() ?? json;
    }
}
