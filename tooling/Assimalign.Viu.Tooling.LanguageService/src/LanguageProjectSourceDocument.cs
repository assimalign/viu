namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// One sibling source document of a resolved project context ([V01.01.12.23], #259): a C# source
/// file or a single-file component that participates in the same compilation as the open document.
/// </summary>
/// <param name="FilePath">The absolute file path of the source document.</param>
/// <param name="Text">The complete document text as read from disk by the host.</param>
/// <param name="IsComponent">
/// <see langword="true"/> for a single-file component (<c>.viu</c>/<c>.vue</c>) whose C# is
/// produced by projection; <see langword="false"/> for a plain C# source file.
/// </param>
public sealed record LanguageProjectSourceDocument(
    string FilePath,
    string Text,
    bool IsComponent);
