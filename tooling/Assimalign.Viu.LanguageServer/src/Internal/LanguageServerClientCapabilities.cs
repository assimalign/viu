using System.Text.Json;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// The client capabilities this server honors, captured from the <c>initialize</c> request. An
/// omitted <c>contentFormat</c>/<c>documentationFormat</c> means plaintext-only per the Language
/// Server Protocol, and Visual Studio advertises plaintext hover content
/// (https://github.com/microsoft/VSExtensibility/issues/271 — VS renders hover markdown literally),
/// so every capability defaults to the least-capable client.
/// </summary>
/// <param name="HoverSupportsMarkdown">Whether <c>textDocument.hover.contentFormat</c> includes <c>markdown</c>.</param>
/// <param name="CompletionDocumentationSupportsMarkdown">Whether <c>textDocument.completion.completionItem.documentationFormat</c> includes <c>markdown</c>.</param>
/// <param name="CompletionSupportsSnippets">
/// Whether <c>textDocument.completion.completionItem.snippetSupport</c> is <see langword="true"/>.
/// A client that does not advertise it has no way to expand <c>$1</c>/<c>$0</c> and would insert them
/// as literal text, so the server sends such a client the placeholder-free rendering instead.
/// </param>
/// <param name="SupportsHierarchicalDocumentSymbols">Whether <c>textDocument.documentSymbol.hierarchicalDocumentSymbolSupport</c> is <see langword="true"/>.</param>
/// <param name="SupportsCodeActionLiterals">Whether <c>textDocument.codeAction.codeActionLiteralSupport</c> is present.</param>
/// <param name="SupportsViuSemanticClassificationNotifications">
/// Whether the Viu client opted into exact C# classification-name publications through
/// <c>initializationOptions.semanticClassificationNotifications</c>.
/// </param>
internal sealed record LanguageServerClientCapabilities(
    bool HoverSupportsMarkdown,
    bool CompletionDocumentationSupportsMarkdown,
    bool CompletionSupportsSnippets,
    bool SupportsHierarchicalDocumentSymbols,
    bool SupportsCodeActionLiterals,
    bool SupportsViuSemanticClassificationNotifications)
{
    /// <summary>Gets the plaintext-only default used before (or without) an <c>initialize</c> request.</summary>
    internal static LanguageServerClientCapabilities Default { get; } =
        new(
            HoverSupportsMarkdown: false,
            CompletionDocumentationSupportsMarkdown: false,
            CompletionSupportsSnippets: false,
            SupportsHierarchicalDocumentSymbols: false,
            SupportsCodeActionLiterals: false,
            SupportsViuSemanticClassificationNotifications: false);

    /// <summary>Reads the honored capabilities from the <c>initialize</c> request parameters.</summary>
    /// <param name="initializeParameters">The <c>initialize</c> request's <c>params</c> element.</param>
    /// <returns>The captured capabilities; missing properties at any level read as unsupported.</returns>
    internal static LanguageServerClientCapabilities Read(JsonElement initializeParameters)
    {
        var supportsViuSemanticClassificationNotifications =
            TryGetObject(initializeParameters, "initializationOptions", out var initializationOptions) &&
            initializationOptions.TryGetProperty(
                "semanticClassificationNotifications",
                out var semanticClassificationNotifications) &&
            semanticClassificationNotifications.ValueKind == JsonValueKind.True;

        if (!TryGetObject(initializeParameters, "capabilities", out var capabilities) ||
            !TryGetObject(capabilities, "textDocument", out var textDocument))
        {
            return Default with
            {
                SupportsViuSemanticClassificationNotifications =
                    supportsViuSemanticClassificationNotifications,
            };
        }

        var hoverSupportsMarkdown =
            TryGetObject(textDocument, "hover", out var hover) &&
            ContainsMarkdown(hover, "contentFormat");
        var completionDocumentationSupportsMarkdown = false;
        var completionSupportsSnippets = false;
        if (TryGetObject(textDocument, "completion", out var completion) &&
            TryGetObject(completion, "completionItem", out var completionItem))
        {
            completionDocumentationSupportsMarkdown =
                ContainsMarkdown(completionItem, "documentationFormat");
            completionSupportsSnippets =
                completionItem.TryGetProperty("snippetSupport", out var snippetSupport) &&
                snippetSupport.ValueKind == JsonValueKind.True;
        }
        var supportsHierarchicalDocumentSymbols =
            TryGetObject(textDocument, "documentSymbol", out var documentSymbol) &&
            documentSymbol.TryGetProperty(
                "hierarchicalDocumentSymbolSupport",
                out var hierarchicalSupport) &&
            hierarchicalSupport.ValueKind == JsonValueKind.True;
        var supportsCodeActionLiterals =
            TryGetObject(textDocument, "codeAction", out var codeAction) &&
            TryGetObject(codeAction, "codeActionLiteralSupport", out _);

        return new LanguageServerClientCapabilities(
            hoverSupportsMarkdown,
            completionDocumentationSupportsMarkdown,
            completionSupportsSnippets,
            supportsHierarchicalDocumentSymbols,
            supportsCodeActionLiterals,
            supportsViuSemanticClassificationNotifications);
    }

    private static bool TryGetObject(
        JsonElement parent,
        string propertyName,
        out JsonElement property)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out property) &&
            property.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        property = default;
        return false;
    }

    private static bool ContainsMarkdown(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var formats) ||
            formats.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var format in formats.EnumerateArray())
        {
            if (format.ValueKind == JsonValueKind.String &&
                format.GetString() == "markdown")
            {
                return true;
            }
        }

        return false;
    }
}
