using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The result of a directive transform: the vnode properties it contributes and, optionally, the runtime
/// directive it needs applied via <c>withDirectives</c>. The C# port of Vue 3.5's
/// <c>DirectiveTransformResult</c> (<c>@vue/compiler-core</c> <c>transform.ts</c>).
/// </summary>
public sealed record DirectiveTransformResult
{
    /// <summary>The properties this directive contributes to the element's props object.</summary>
    public required IReadOnlyList<Property> Properties { get; init; }

    /// <summary>
    /// The runtime directive helper to apply (e.g. <c>vShow</c>, <c>vModelText</c>), or <see langword="null"/>
    /// when the directive contributes only props. Mirrors upstream's <c>needRuntime</c> symbol case.
    /// </summary>
    public RuntimeHelper? NeedRuntime { get; init; }

    /// <summary>
    /// Whether the directive needs a resolved (user) runtime directive without a specific helper symbol,
    /// mirroring upstream's <c>needRuntime: true</c> boolean case.
    /// </summary>
    public bool NeedsResolvedDirective { get; init; }
}
