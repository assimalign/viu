using System.Collections.Generic;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Provides editor-neutral language features for open <c>.viu</c> documents.
/// </summary>
public interface IViuLanguageService
{
    /// <summary>Opens or replaces a document in the language-service workspace.</summary>
    /// <param name="documentUri">The document URI used by the editor.</param>
    /// <param name="text">The complete document text.</param>
    /// <param name="version">The editor-supplied document version.</param>
    void OpenDocument(string documentUri, string text, int? version);

    /// <summary>Applies one or more ordered content changes to an open document.</summary>
    /// <param name="documentUri">The document URI used by the editor.</param>
    /// <param name="version">The new editor-supplied document version.</param>
    /// <param name="changes">The full-document or ranged changes, in application order.</param>
    /// <returns>
    /// <see langword="true"/> when the document was open and the changes were applied;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    bool ChangeDocument(string documentUri, int? version, IReadOnlyList<LanguageDocumentChange> changes);

    /// <summary>Closes a document and releases its cached parse result.</summary>
    /// <param name="documentUri">The document URI used by the editor.</param>
    /// <returns><see langword="true"/> when an open document was removed.</returns>
    bool CloseDocument(string documentUri);

    /// <summary>Gets parser diagnostics for an open document.</summary>
    /// <param name="documentUri">The document URI used by the editor.</param>
    /// <returns>The current diagnostics, or an empty list when the document is not open.</returns>
    IReadOnlyList<LanguageDiagnostic> GetDiagnostics(string documentUri);

    /// <summary>Gets context-sensitive completion items at a document position.</summary>
    /// <param name="documentUri">The document URI used by the editor.</param>
    /// <param name="position">The zero-based editor position.</param>
    /// <returns>The completion items, or an empty list when the document is not open.</returns>
    IReadOnlyList<LanguageCompletionItem> GetCompletions(string documentUri, LanguagePosition position);

    /// <summary>Gets documentation for the language token at a document position.</summary>
    /// <param name="documentUri">The document URI used by the editor.</param>
    /// <param name="position">The zero-based editor position.</param>
    /// <returns>The hover result, or <see langword="null"/> when the token is unknown.</returns>
    LanguageHover? GetHover(string documentUri, LanguagePosition position);
}
