namespace Assimalign.Viu.Syntax.JavaScript;

/// <summary>
/// The program root node — ECMA-262's <c>Program</c> (a whole script or module,
/// https://tc39.es/ecma262/#sec-ecmascript-language-scripts-and-modules). In the current scaffold it
/// carries the raw program <see cref="Content"/> spanning the whole source; the child statement list
/// replaces raw content when statement-level parsing lands.
/// </summary>
public sealed record JavaScriptProgramNode : JavaScriptSyntaxNode
{
    /// <summary>The raw program text — the whole parsed source, pending statement-level parsing.</summary>
    public required string Content { get; init; }

    /// <inheritdoc />
    public override JavaScriptSyntaxNodeKind Kind => JavaScriptSyntaxNodeKind.Program;
}
