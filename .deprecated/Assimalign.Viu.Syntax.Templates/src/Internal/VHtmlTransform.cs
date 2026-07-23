using System;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The <c>v-html</c> directive transform: compiles to an <c>innerHTML</c> prop and raises the child-conflict
/// diagnostic when the element also has template children. The C# port of Vue 3.5's <c>transformVHtml</c>
/// (<c>@vue/compiler-dom</c> <c>transforms/vHtml.ts</c>).
/// </summary>
internal static class VHtmlTransform
{
    /// <summary>The directive transform delegate.</summary>
    public static DirectiveTransformResult Transform(
        DirectiveNode directive,
        ElementNode element,
        TransformContext context,
        Func<DirectiveTransformResult, DirectiveTransformResult>? augmentor)
    {
        var expression = directive.Expression;
        if (expression is null)
        {
            context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVHtmlNoExpression, directive.Location));
        }

        if (element.Children.Count > 0)
        {
            context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVHtmlWithChildren, directive.Location));
            if (context.TryGetWorkingChildren(element, out var workingChildren))
            {
                workingChildren.Clear();
            }
        }

        return new DirectiveTransformResult
        {
            Properties = new[]
            {
                Ir.ObjectProperty("innerHTML", expression ?? Ir.SimpleExpression(string.Empty, true)),
            },
        };
    }
}
