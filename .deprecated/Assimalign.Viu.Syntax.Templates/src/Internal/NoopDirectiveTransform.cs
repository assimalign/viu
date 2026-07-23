using System;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A directive transform that contributes nothing — used for <c>v-cloak</c>, which needs no runtime prop at
/// compile time (the runtime removes the <c>v-cloak</c> attribute on mount; CSS hides <c>[v-cloak]</c> until
/// then). The C# port of Vue 3.5's <c>noopDirectiveTransform</c> (<c>@vue/compiler-core</c>
/// <c>transforms/noopDirectiveTransform.ts</c>).
/// </summary>
internal static class NoopDirectiveTransform
{
    /// <summary>The directive transform delegate.</summary>
    public static DirectiveTransformResult Transform(
        DirectiveNode directive,
        ElementNode element,
        TransformContext context,
        Func<DirectiveTransformResult, DirectiveTransformResult>? augmentor)
        => new() { Properties = Array.Empty<Property>() };
}
