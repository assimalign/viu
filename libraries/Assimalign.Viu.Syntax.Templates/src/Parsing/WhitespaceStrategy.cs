namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// How the parser handles insignificant whitespace between nodes. Condensing is the default because
/// whitespace-only text between elements would otherwise become render nodes that cost patches and
/// never change.
/// </summary>
public enum WhitespaceStrategy
{
    /// <summary>
    /// Condense runs of whitespace to a single space and drop insignificant whitespace-only text
    /// nodes the default.
    /// </summary>
    Condense = 0,

    /// <summary>Preserve whitespace text nodes as authored.</summary>
    Preserve = 1,
}
