namespace Assimalign.Vue.Syntax;

/// <summary>
/// The shared root of every <c>Assimalign.Vue.Syntax.*</c> node hierarchy: an immutable,
/// value-comparable record carrying the node's <see cref="SourceLocation"/>. Each language library
/// derives its own abstract root from it — the template parser's <c>TemplateSyntaxNode</c> (the C#
/// port of Vue 3.5's <c>Node</c>, <c>@vue/compiler-core</c> <c>ast.ts</c>), the single-file-component
/// parser's <c>SingleFileComponentBlock</c>, and the browser-language scaffolds (<c>CssSyntaxNode</c>,
/// <c>HtmlSyntaxNode</c>, <c>JavaScriptSyntaxNode</c>) — so every parser shares one located,
/// structurally-equatable node contract that <see cref="SyntaxParser"/> and the code-generation layer
/// can consume without knowing the language.
/// </summary>
/// <remarks>
/// <para>
/// Records give the whole tree structural equality, the incremental-caching contract of the derived
/// parsers ([V01.01.05.01]/[V01.01.06.01]): parsing equal input twice yields equal nodes, so a Roslyn
/// incremental generator can cache on the parse output, and any content or location difference makes the
/// nodes unequal. Preserving exact equality semantics (record equality plus <see cref="SyntaxList{T}"/>'s
/// element-wise equality) is load-bearing — a reference-comparing collection would silently defeat the
/// cache.
/// </para>
/// <para>
/// <see cref="Location"/> positions are relative to whatever source string the owning parser was handed:
/// block content for the template compiler, the whole <c>.viu</c> file for the single-file-component
/// parser. Mapping between those coordinate spaces is the consumer's job ([V01.01.06.03]).
/// </para>
/// </remarks>
public abstract record SyntaxNode
{
    /// <summary>The source range this node spans, with the exact original slice.</summary>
    public required SourceLocation Location { get; init; }

    /// <summary>
    /// The node's kind discriminator as an integer, projected from the derived hierarchy's enum-typed
    /// kind. Mirrors Roslyn's <c>SyntaxNode.RawKind</c> and this base's <see cref="Diagnostic.RawCode"/>:
    /// the base deliberately does <em>not</em> define a shared kind enum, because each language owns its
    /// own catalog (the template parser's <c>NodeType</c> is pinned numerically to
    /// <c>@vue/compiler-core</c>'s <c>NodeTypes</c>; the single-file-component parser's block kinds are
    /// Vuecs-defined) and a closed base enum could not be extended by additional language libraries or
    /// custom registered parsers. Language-agnostic infrastructure switches on this projection; typed
    /// consumers use the derived hierarchy's own kind property.
    /// </summary>
    public abstract int RawKind { get; }
}
