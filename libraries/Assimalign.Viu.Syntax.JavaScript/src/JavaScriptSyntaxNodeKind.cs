namespace Assimalign.Viu.Syntax.JavaScript;

/// <summary>
/// Discriminates the kinds of node the JavaScript parser produces, following the ECMAScript grammar's
/// top-level shape (ECMA-262,
/// https://tc39.es/ecma262/#sec-ecmascript-language-scripts-and-modules). The catalog is Viu-defined
/// (there is no upstream Vue numbering to pin) and grows as statement-level parsing lands.
/// </summary>
public enum JavaScriptSyntaxNodeKind
{
    /// <summary>The program root — a whole script or module.</summary>
    Program = 0,

    /// <summary>A statement or declaration.</summary>
    Statement = 1,

    /// <summary>An expression.</summary>
    Expression = 2,

    /// <summary>A comment.</summary>
    Comment = 3,
}
