using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The result of a directive transform: the render-node properties it contributes and, optionally, the
/// runtime directive it needs applied via <c>withDirectives</c>. A directive that compiles away entirely
/// contributes properties and no runtime directive.
/// </summary>
public sealed record DirectiveTransformResult
{
    /// <summary>The properties this directive contributes to the element's props object.</summary>
    public required IReadOnlyList<Property> Properties { get; init; }

    /// <summary>
    /// The runtime directive helper to apply (e.g. <c>vShow</c>, <c>vModelText</c>), or <see langword="null"/>
    /// when the directive contributes only props. A directive that compiles away entirely leaves this null.
    /// </summary>
    public RuntimeHelper? NeedRuntime { get; init; }

    /// <summary>
    /// Whether the directive needs a resolved (user) runtime directive without a specific helper symbol,
    /// so the renderer applies the directive at runtime.
    /// </summary>
    public bool NeedsResolvedDirective { get; init; }
}
