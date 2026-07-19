namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// How the parser handles insignificant whitespace between nodes. The C# port of Vue 3.5's
/// <c>whitespace</c> parser option (<c>@vue/compiler-core</c> <c>options.ts</c>).
/// </summary>
public enum WhitespaceStrategy
{
    /// <summary>
    /// Condense runs of whitespace to a single space and drop insignificant whitespace-only text
    /// nodes (upstream <c>'condense'</c>, the default).
    /// </summary>
    Condense = 0,

    /// <summary>Preserve whitespace text nodes as authored (upstream <c>'preserve'</c>).</summary>
    Preserve = 1,
}
