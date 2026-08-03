using System.Collections.Generic;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// One semantic script completion answer from <see cref="ScriptSemanticEngine"/> ([V01.01.12.23],
/// #259): the symbol-bound items plus the request's classification, so the service knows which
/// scaffold catalog still appends behind them — the member catalogs after a
/// <c>Context.</c>/<c>Reactive.</c> access, the general scaffold and keyword catalogs in an
/// identifier position.
/// </summary>
/// <param name="Items">The bound completion items, in the engine's deterministic order.</param>
/// <param name="IsMemberAccess">
/// Whether the request completed a member access (<c>expr.</c>) rather than an identifier
/// position.
/// </param>
/// <param name="MemberAccessTargetText">
/// The accessed expression's source text (for example <c>Context</c>), or <see langword="null"/>
/// for an identifier position.
/// </param>
internal sealed record ScriptSemanticCompletionResult(
    IReadOnlyList<LanguageCompletionItem> Items,
    bool IsMemberAccess,
    string? MemberAccessTargetText);
