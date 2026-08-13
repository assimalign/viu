using Microsoft.CodeAnalysis.Text;

namespace Assimalign.Viu.LanguageService;

/// <summary>One classified identifier in a generated or plain C# syntax tree.</summary>
/// <param name="Span">The syntax-tree span.</param>
/// <param name="ClassificationTypeName">The C# editor classification-type name.</param>
internal readonly record struct ScriptSemanticClassification(
    TextSpan Span,
    string ClassificationTypeName);
