using System;
using System.Text.Json;

namespace Assimalign.Viu.UtilityCss.VisualStudio;

/// <summary>
/// Restricts messages inherited through Visual Studio's HTML content-type hierarchy to standalone
/// HTML documents and HTML projections.
/// </summary>
internal static class UtilityCssDocumentMessageFilter
{
    /// <summary>Determines whether a document message may reach the utility-CSS language server.</summary>
    internal static bool ShouldForward(JsonElement methodParameters)
    {
        if (methodParameters.ValueKind != JsonValueKind.Object ||
            !methodParameters.TryGetProperty("textDocument", out var textDocument) ||
            textDocument.ValueKind != JsonValueKind.Object ||
            !textDocument.TryGetProperty("uri", out var uriElement) ||
            uriElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? documentUri = uriElement.GetString();
        if (documentUri is null || string.IsNullOrWhiteSpace(documentUri))
        {
            return false;
        }

        string documentPath = documentUri;
        if (Uri.TryCreate(documentUri, UriKind.Absolute, out var absoluteUri))
        {
            documentPath = absoluteUri.IsFile
                ? absoluteUri.LocalPath
                : Uri.UnescapeDataString(absoluteUri.AbsolutePath);
        }

        return documentPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            documentPath.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);
    }
}
