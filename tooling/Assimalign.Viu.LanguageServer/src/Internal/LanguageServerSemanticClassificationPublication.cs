using System;
using System.Text.Json.Nodes;

using Assimalign.Viu.LanguageService;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Serializes Viu's exact C# classification names for the thin Visual Studio classification
/// adapter. The standard semantic-token vocabulary remains available to ordinary Language Server
/// Protocol clients, while this opt-in message avoids collapsing fields, locals, and constants into
/// one <c>variable</c> token. [V01.01.12.07.11]
/// </summary>
internal static class LanguageServerSemanticClassificationPublication
{
    /// <summary>The custom notification method understood by the Viu Visual Studio client.</summary>
    internal const string MethodName = "viu/publishSemanticClassifications";

    /// <summary>Creates one versioned, authored-position classification publication.</summary>
    internal static JsonObject Create(
        string documentUri,
        LanguageClassificationSnapshot? snapshot,
        bool isClosed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);

        var classifications = new JsonArray();
        if (snapshot is not null)
        {
            foreach (var classification in snapshot.Classifications)
            {
                classifications.Add(
                    (JsonNode)new JsonObject
                    {
                        ["range"] = CreateRange(classification.Range),
                        ["classificationTypeName"] = classification.ClassificationTypeName,
                    });
            }
        }

        return new JsonObject
        {
            ["uri"] = documentUri,
            ["version"] = snapshot?.Version is int version
                ? JsonValue.Create(version)
                : null,
            ["textChecksum"] = snapshot?.TextChecksum,
            ["isClosed"] = isClosed,
            ["classifications"] = classifications,
        };
    }

    private static JsonObject CreateRange(LanguageRange range)
        => new()
        {
            ["start"] = CreatePosition(range.Start),
            ["end"] = CreatePosition(range.End),
        };

    private static JsonObject CreatePosition(LanguagePosition position)
        => new()
        {
            ["line"] = position.Line,
            ["character"] = position.Character,
        };
}
