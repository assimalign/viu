using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Transports an explicitly serialized JSON payload between server rendering and a client host
/// before hydration begins.
/// </summary>
/// <remarks>
/// The codec depends only on raw JSON. State packages provide their own schema and source-generated
/// serializers, while this transport validates the JSON, emits an inert script island, and requires
/// a caller-supplied <see cref="JsonTypeInfo{T}"/> for typed restoration. Specified by
/// <c>[SSR-7]</c> and <c>[EXE-4]</c>.
/// </remarks>
public static class SsrStateIsland
{
    /// <summary>Gets the host selector for locating the emitted state payload before hydration.</summary>
    public const string Selector = "script[data-viu-state]";

    /// <summary>Creates inert script markup containing normalized, HTML-safe JSON.</summary>
    /// <param name="json">The explicitly serialized JSON payload.</param>
    /// <returns>The complete state-island markup.</returns>
    /// <exception cref="JsonException">The payload is not valid JSON.</exception>
    public static string CreateMarkup(string json) =>
        string.Concat(
            "<script type=\"application/json\" data-viu-state>",
            NormalizePayload(json),
            "</script>");

    /// <summary>Validates and normalizes a raw JSON payload with HTML-safe escaping.</summary>
    /// <param name="json">The explicitly serialized JSON payload.</param>
    /// <returns>The validated payload text used both for the island and client handoff.</returns>
    /// <exception cref="JsonException">The payload is not valid JSON.</exception>
    public static string NormalizePayload(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using JsonDocument document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(
            document.RootElement,
            SsrStateIslandJsonSerializerContext.Default.JsonElement);
    }

    /// <summary>Writes and flushes one state island through a borrowed text writer.</summary>
    /// <param name="writer">The borrowed host or renderer writer.</param>
    /// <param name="json">The explicitly serialized JSON payload.</param>
    /// <param name="cancellationToken">Cancellation propagated from the host request.</param>
    /// <returns>A task completing only after the destination flush completes.</returns>
    public static async Task WriteAsync(
        TextWriter writer,
        string json,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        string markup = CreateMarkup(json);
        await writer.WriteAsync(markup.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deserializes extracted island text through caller-supplied source-generated metadata before
    /// the client host begins hydration.
    /// </summary>
    /// <typeparam name="T">The explicitly registered payload schema.</typeparam>
    /// <param name="json">The text content extracted through <see cref="Selector"/>.</param>
    /// <param name="typeInformation">The source-generated JSON metadata for the payload.</param>
    /// <returns>The restored payload value.</returns>
    /// <exception cref="JsonException">The payload does not match the registered schema.</exception>
    public static T? Deserialize<T>(string json, JsonTypeInfo<T> typeInformation)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(typeInformation);
        return JsonSerializer.Deserialize(json, typeInformation);
    }
}
