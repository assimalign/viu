using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Provides editor-neutral language features for open Viu single-file-component documents.
/// </summary>
/// <remarks>
/// Mutations (<see cref="OpenDocument"/>, <see cref="ChangeDocument"/>, <see cref="CloseDocument"/>)
/// apply serially in call order. Reads compute over a snapshot captured at call time, so a read that
/// runs concurrently with document synchronization may observe state newer than the moment it was
/// issued (monotonic reads) — the accepted Language Server Protocol idiom, which clients compensate
/// for with document versions and request cancellation.
/// </remarks>
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
    /// <param name="cancellationToken">
    /// The token that cancels the computation. Cancellation is cooperative; a canceled call throws
    /// <see cref="System.OperationCanceledException"/> and leaves no service state modified.
    /// </param>
    /// <returns>The current diagnostics, or an empty list when the document is not open.</returns>
    IReadOnlyList<LanguageDiagnostic> GetDiagnostics(
        string documentUri,
        CancellationToken cancellationToken = default);

    /// <summary>Gets context-sensitive completion items at a document position.</summary>
    /// <param name="documentUri">The document URI used by the editor.</param>
    /// <param name="position">The zero-based editor position.</param>
    /// <param name="cancellationToken">
    /// The token that cancels the computation. Cancellation is cooperative; a canceled call throws
    /// <see cref="System.OperationCanceledException"/> and leaves no service state modified.
    /// </param>
    /// <returns>The completion items, or an empty list when the document is not open.</returns>
    IReadOnlyList<LanguageCompletionItem> GetCompletions(
        string documentUri,
        LanguagePosition position,
        CancellationToken cancellationToken = default);

    /// <summary>Gets documentation for the language token at a document position.</summary>
    /// <param name="documentUri">The document URI used by the editor.</param>
    /// <param name="position">The zero-based editor position.</param>
    /// <param name="cancellationToken">
    /// The token that cancels the computation. Cancellation is cooperative; a canceled call throws
    /// <see cref="System.OperationCanceledException"/> and leaves no service state modified.
    /// </param>
    /// <returns>The hover result, or <see langword="null"/> when the token is unknown.</returns>
    LanguageHover? GetHover(
        string documentUri,
        LanguagePosition position,
        CancellationToken cancellationToken = default);

    /// <summary>Computes the deferred documentation body for a previously returned completion item.</summary>
    /// <param name="documentUri">The document URI used by the editor.</param>
    /// <param name="completionLabel">The label of the completion item being resolved.</param>
    /// <param name="cancellationToken">
    /// The token that cancels the computation. Cancellation is cooperative; a canceled call throws
    /// <see cref="System.OperationCanceledException"/> and leaves no service state modified.
    /// </param>
    /// <returns>
    /// The Markdown documentation, or <see langword="null"/> when the label is not a resolvable
    /// candidate.
    /// </returns>
    string? ResolveCompletionDocumentation(
        string documentUri,
        string completionLabel,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the hierarchical block outline for an open document.</summary>
    /// <param name="documentUri">The document URI used by the editor.</param>
    /// <param name="cancellationToken">
    /// The token that cancels the computation. Cancellation is cooperative; a canceled call throws
    /// <see cref="System.OperationCanceledException"/> and leaves no service state modified.
    /// </param>
    /// <returns>The block symbols in source order, or an empty list when the document is not open.</returns>
    IReadOnlyList<LanguageDocumentSymbol> GetDocumentSymbols(
        string documentUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the foldable ranges for an open document: one range per multi-line block container, plus
    /// one per multi-line element inside the template block ([V01.01.12.07.07]) — nested elements and
    /// nested <c>&lt;template&gt;</c> fragments included — plus one per multi-line C# construct inside
    /// a script block ([V01.01.12.07.10]): member and statement blocks, accessor lists, object and
    /// collection initializers, collection expressions, nested type and switch bodies, and
    /// <c>#region</c> pairs.
    /// <para>
    /// Two collapse conventions apply, one per family, because a collapsed template reads as markup and
    /// a collapsed script reads as C#. A block-container range and a template-element range end one line
    /// <em>above</em> the closing delimiter, so <c>}</c> and <c>&lt;/div&gt;</c> stay visible. A script
    /// construct's range instead starts on the line of the token before its opening delimiter and ends
    /// <em>on</em> the closing delimiter's line, so the construct collapses beside its signature with
    /// both delimiters hidden — what the C# editor does for a <c>.cs</c> file ([V01.01.12.07.10]).
    /// Ranges still nest: a construct closes strictly inside its own section.
    /// </para>
    /// A single-line construct folds nothing; neither does a self-closing element, an expression-bodied
    /// member (no delimiter pair), or a construct the parse had to recover from missing markup or
    /// missing C#.
    /// </summary>
    /// <param name="documentUri">The document URI used by the editor.</param>
    /// <param name="cancellationToken">
    /// The token that cancels the computation. Cancellation is cooperative; a canceled call throws
    /// <see cref="System.OperationCanceledException"/> and leaves no service state modified.
    /// </param>
    /// <returns>
    /// The folding ranges in document order — a container's range ahead of the ranges nested inside
    /// it — or an empty list when the document is not open. The result is not truncated: a document
    /// contributes one range per foldable construct, however many that is.
    /// </returns>
    IReadOnlyList<LanguageFoldingRange> GetFoldingRanges(
        string documentUri,
        CancellationToken cancellationToken = default);

    /// <summary>Gets quick fixes for diagnostics intersecting a range in an open document.</summary>
    /// <param name="documentUri">The document URI used by the editor.</param>
    /// <param name="range">The zero-based document range the editor is requesting actions for.</param>
    /// <param name="cancellationToken">
    /// The token that cancels the computation. Cancellation is cooperative; a canceled call throws
    /// <see cref="System.OperationCanceledException"/> and leaves no service state modified.
    /// </param>
    /// <returns>The applicable code actions, or an empty list when none apply.</returns>
    IReadOnlyList<LanguageCodeAction> GetCodeActions(
        string documentUri,
        LanguageRange range,
        CancellationToken cancellationToken = default);
}
