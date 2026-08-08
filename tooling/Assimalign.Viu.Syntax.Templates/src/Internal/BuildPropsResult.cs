using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The result of building an element's props: the props expression, the runtime directives to apply, the
/// accumulated patch flag, the dynamic prop names, and whether the element must be forced into a block.
/// </summary>
internal sealed record BuildPropsResult
{
    /// <summary>The props expression (object, merge call, or single binding), or <see langword="null"/>.</summary>
    public TemplateSyntaxNode? Props { get; init; }

    /// <summary>The runtime directives to apply via <c>withDirectives</c>.</summary>
    public required IReadOnlyList<DirectiveNode> Directives { get; init; }

    /// <summary>The accumulated patch flag.</summary>
    public PatchFlags PatchFlag { get; init; }

    /// <summary>The dynamic prop names (consumed by [V01.01.05.06]).</summary>
    public required IReadOnlyList<string> DynamicPropNames { get; init; }

    /// <summary>Whether the element must be forced into an optimization block.</summary>
    public bool ShouldUseBlock { get; init; }
}
