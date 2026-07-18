namespace Assimalign.Vue.Syntax.Generators;

/// <summary>
/// The value-equatable read of one <c>.viu</c> additional file: its path, full text, and the resolved
/// C# names. This is the first pipeline stage's output — a pure data record with no Roslyn
/// <c>AdditionalText</c>, <c>SourceText</c>, or compilation reference — so downstream parse/codegen
/// stages re-run only when the file's text or resolved names actually change, keeping the incremental
/// pipeline and IDE responsiveness intact.
/// </summary>
/// <param name="FilePath">The absolute <c>.viu</c> file path (the diagnostic and hint-name anchor).</param>
/// <param name="FileName">The leaf file name, used verbatim in the generated scaffold header.</param>
/// <param name="Text">The full <c>.viu</c> source text.</param>
/// <param name="Namespace">The resolved containing namespace, or <see langword="null"/> for the global namespace.</param>
/// <param name="ClassName">The resolved generated partial class name.</param>
/// <param name="HintName">The resolved, unique <c>AddSource</c> hint name.</param>
internal readonly record struct SingleFileComponentFile(
    string FilePath,
    string FileName,
    string Text,
    string? Namespace,
    string ClassName,
    string HintName);
