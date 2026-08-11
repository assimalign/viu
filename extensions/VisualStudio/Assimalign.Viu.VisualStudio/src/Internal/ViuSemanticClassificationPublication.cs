using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// The editor-free representation of one custom semantic-classification notification.
/// </summary>
internal sealed record ViuSemanticClassificationPublication(
    string DocumentIdentifier,
    int? Version,
    string? TextChecksum,
    bool IsClosed,
    IReadOnlyList<ViuSemanticClassification> Classifications)
{
    /// <summary>Parses the server's versioned publication without retaining the JSON document.</summary>
    internal static bool TryParse(
        JsonElement parameters,
        out ViuSemanticClassificationPublication? publication)
    {
        publication = null;
        if (parameters.ValueKind != JsonValueKind.Object ||
            !TryGetString(parameters, "uri", out var documentIdentifier) ||
            !TryGetBoolean(parameters, "isClosed", out var isClosed) ||
            !parameters.TryGetProperty("classifications", out var classificationsElement) ||
            classificationsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        int? version = null;
        if (parameters.TryGetProperty("version", out var versionElement) &&
            versionElement.ValueKind != JsonValueKind.Null)
        {
            if (versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetInt32(out var parsedVersion))
            {
                return false;
            }

            version = parsedVersion;
        }

        string? textChecksum = null;
        if (parameters.TryGetProperty("textChecksum", out var checksumElement) &&
            checksumElement.ValueKind != JsonValueKind.Null)
        {
            if (checksumElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(checksumElement.GetString()))
            {
                return false;
            }

            textChecksum = checksumElement.GetString();
        }

        if (!isClosed && textChecksum is null)
        {
            return false;
        }

        var classifications = new List<ViuSemanticClassification>();
        foreach (var classificationElement in classificationsElement.EnumerateArray())
        {
            if (!TryParseClassification(classificationElement, out var classification))
            {
                return false;
            }

            classifications.Add(classification);
        }

        publication = new ViuSemanticClassificationPublication(
            documentIdentifier,
            version,
            textChecksum,
            isClosed,
            classifications.ToArray());
        return true;
    }

    private static bool TryParseClassification(
        JsonElement element,
        out ViuSemanticClassification classification)
    {
        classification = default;
        if (element.ValueKind != JsonValueKind.Object ||
            !TryGetString(element, "classificationTypeName", out var classificationTypeName) ||
            !element.TryGetProperty("range", out var range) ||
            range.ValueKind != JsonValueKind.Object ||
            !range.TryGetProperty("start", out var start) ||
            !range.TryGetProperty("end", out var end) ||
            !TryGetPosition(start, out var startLine, out var startCharacter) ||
            !TryGetPosition(end, out var endLine, out var endCharacter))
        {
            return false;
        }

        classification = new ViuSemanticClassification(
            startLine,
            startCharacter,
            endLine,
            endCharacter,
            classificationTypeName);
        return classification.IsValid;
    }

    private static bool TryGetPosition(JsonElement element, out int line, out int character)
    {
        line = -1;
        character = -1;
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("line", out var lineElement) &&
            lineElement.ValueKind == JsonValueKind.Number &&
            lineElement.TryGetInt32(out line) &&
            element.TryGetProperty("character", out var characterElement) &&
            characterElement.ValueKind == JsonValueKind.Number &&
            characterElement.TryGetInt32(out character) &&
            line >= 0 &&
            character >= 0;
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            return false;
        }

        value = property.GetString()!;
        return true;
    }

    private static bool TryGetBoolean(
        JsonElement element,
        string propertyName,
        out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }
}
