namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The root of a parsed template, carrying only what the parser produces: the top-level children and
/// the original source. Transform and code-generation bookkeeping is deliberately not on this node —
/// it belongs to <see cref="TransformResult"/>, so a parse result stays a pure function of its input
/// and the incremental cache never sees a later stage's state.
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
