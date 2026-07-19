namespace Assimalign.Viu.Syntax.JavaScript;

/// <summary>
/// The base of every JavaScript syntax node: an immutable, value-comparable record rooting the script
/// tree on the shared <see cref="SyntaxNode"/> contract, with the JavaScript-specific
/// <see cref="JavaScriptSyntaxNodeKind"/> discriminator. Node categories follow the ECMAScript
/// grammar's top-level shape (ECMA-262 <c>Program</c>/<c>Script</c>/<c>Module</c>,
/// https://tc39.es/ecma262/#sec-ecmascript-language-scripts-and-modules).
/// </summary>
/// <remarks>
/// This hierarchy covers the JavaScript that Viu build tooling reads or emits around the JS-interop
/// boundary (interop glue modules, host-page scripts) — component logic itself is C# and is Roslyn's
/// domain, and the template compiler's expression work stays in
/// <c>Assimalign.Viu.Syntax.Templates</c>. Scaffold: the tree currently carries only the raw
/// <see cref="JavaScriptProgramNode"/> produced by <see cref="JavaScriptSyntaxParser"/>.
/// </remarks>
public abstract record JavaScriptSyntaxNode : SyntaxNode
{
    /// <summary>The node kind discriminator.</summary>
    public abstract JavaScriptSyntaxNodeKind Kind { get; }

    /// <inheritdoc />
    public sealed override int RawKind => (int)Kind;
}
