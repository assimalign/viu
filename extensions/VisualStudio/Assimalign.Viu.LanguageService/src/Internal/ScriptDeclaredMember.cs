namespace Assimalign.Viu.LanguageService;

/// <summary>
/// One member declared in a component's <c>@script</c> block, extracted by
/// <see cref="ScriptDeclarationReader"/> for declaration-aware completion
/// ([V01.01.12.07.04], #261).
/// </summary>
/// <param name="Name">
/// The declared identifier exactly as authored (<c>Identifier.Text</c>, keeping the <c>@</c> of a
/// verbatim identifier so the insert text round-trips).
/// </param>
/// <param name="Kind">The completion kind: field, property, or method.</param>
/// <param name="Detail">The declared type text, or the method signature.</param>
/// <param name="DocumentationSummary">
/// The first non-empty <c>///</c> summary text line, or <see langword="null"/> when the member
/// carries no documentation comment.
/// </param>
internal sealed record ScriptDeclaredMember(
    string Name,
    LanguageCompletionItemKind Kind,
    string Detail,
    string? DocumentationSummary);
