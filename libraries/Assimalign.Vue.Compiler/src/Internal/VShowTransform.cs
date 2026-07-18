using System;
using System.Collections.Generic;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// The <c>v-show</c> directive transform: it contributes no props and requests the runtime <c>vShow</c>
/// directive. The C# port of Vue 3.5's <c>transformShow</c> (<c>@vue/compiler-dom</c>
/// <c>transforms/vShow.ts</c>). See https://vuejs.org/api/built-in-directives.html#v-show.
/// </summary>
internal static class VShowTransform
{
    /// <summary>The directive transform delegate.</summary>
    public static DirectiveTransformResult Transform(
        DirectiveNode directive,
        ElementNode element,
        TransformContext context,
        Func<DirectiveTransformResult, DirectiveTransformResult>? augmentor)
    {
        if (directive.Expression is null)
        {
            context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVShowNoExpression, directive.Location));
        }

        return new DirectiveTransformResult
        {
            Properties = Array.Empty<Property>(),
            NeedRuntime = context.Helper(HelperNames.VShow),
        };
    }
}
