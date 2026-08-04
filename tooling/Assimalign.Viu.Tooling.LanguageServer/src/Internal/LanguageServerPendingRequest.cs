using System.Text.Json.Nodes;

using Assimalign.Viu.Tooling.LanguageService;

namespace Assimalign.Viu.Tooling.LanguageServer;

/// <summary>
/// A feature request parsed completely on the message-loop thread before it is dispatched to a
/// request task ([V01.01.12.23], #259). The record is deliberately independent of the pooled
/// <see cref="System.Text.Json.JsonDocument"/> the loop recycles at the end of each iteration: it
/// carries only strings, positions, ranges, and cloned <see cref="JsonNode"/> values, so a
/// dispatched task can never read recycled buffer memory through a captured
/// <see cref="System.Text.Json.JsonElement"/>.
/// </summary>
internal sealed class LanguageServerPendingRequest
{
    /// <summary>Gets the JSON-RPC method name.</summary>
    internal required string Method { get; init; }

    /// <summary>Gets the request identifier as a JSON node detached from the read buffer.</summary>
    internal required JsonNode? Identifier { get; init; }

    /// <summary>
    /// Gets the identifier's raw JSON text — the cancellation-registry key, which keeps the
    /// numeric identifier <c>1</c> and the string identifier <c>"1"</c> distinct per JSON-RPC.
    /// </summary>
    internal required string IdentifierKey { get; init; }

    /// <summary>Gets the client capabilities captured on the loop thread at dispatch time.</summary>
    internal required LanguageServerClientCapabilities ClientCapabilities { get; init; }

    /// <summary>Gets the document URI the request targets.</summary>
    internal required string DocumentUri { get; init; }

    /// <summary>Gets the position for completion and hover requests.</summary>
    internal LanguagePosition Position { get; init; }

    /// <summary>Gets the range for code-action requests.</summary>
    internal LanguageRange Range { get; init; }

    /// <summary>Gets the completion label for resolve requests.</summary>
    internal string? CompletionLabel { get; init; }

    /// <summary>Gets the cloned completion item a resolve request echoes and enriches.</summary>
    internal JsonObject? ResolveItem { get; init; }
}
