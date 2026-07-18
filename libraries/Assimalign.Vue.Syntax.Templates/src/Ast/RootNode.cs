namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The root of a parsed template. The C# port of Vue 3.5's <c>RootNode</c>
/// (<c>@vue/compiler-core</c> <c>ast.ts</c>), reduced to the members the parser produces (the codegen
/// and transform bookkeeping fields belong to later pipeline stages).
/// </summary>
public sealed record RootNode : TemplateSyntaxNode
{
    /// <summary>The top-level child nodes, after whitespace management.</summary>
    public required SyntaxList<TemplateChildNode> Children { get; init; }

    /// <summary>The full original template source.</summary>
    public required string Source { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.Root;
}
