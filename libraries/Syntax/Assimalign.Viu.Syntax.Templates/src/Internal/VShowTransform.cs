using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The <c>v-show</c> directive transform: it contributes no props and requests the runtime <c>vShow</c>
/// directive.
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
            Properties = Array.Empty<ObjectProperty>(),
            NeedRuntime = context.Helper(HelperNames.VShow),
        };
    }
}
