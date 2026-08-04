using System.Collections.Generic;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>An editor-neutral code action offered for diagnostics in an open document.</summary>
/// <param name="Title">The action title shown to the author.</param>
/// <param name="Kind">The action kind, such as <c>quickfix</c>.</param>
/// <param name="Diagnostics">The diagnostics the action addresses.</param>
/// <param name="Edits">The text edits applied to the requesting document when the action runs.</param>
/// <param name="IsPreferred">Whether the action is the preferred fix for its diagnostics.</param>
public sealed record LanguageCodeAction(
    string Title,
    string Kind,
    IReadOnlyList<LanguageDiagnostic> Diagnostics,
    IReadOnlyList<LanguageTextEdit> Edits,
    bool IsPreferred = false);
