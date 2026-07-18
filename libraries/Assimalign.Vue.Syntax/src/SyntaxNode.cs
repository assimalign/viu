namespace Assimalign.Vue.Syntax;

/// <summary>
/// The shared root of every <c>Assimalign.Vue.Syntax.*</c> node hierarchy: an immutable,
/// value-comparable record carrying the node's <see cref="SourceLocation"/>. The template compiler's
/// <c>TemplateSyntaxNode</c> (the C# port of Vue 3.5's <c>Node</c>, <c>@vue/compiler-core</c>
/// <c>ast.ts</c>) and the single-file-component parser's <c>SingleFileComponentBlock</c> both derive from
/// it, so the two parsers share one located, structurally-equatable node contract.
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
}
