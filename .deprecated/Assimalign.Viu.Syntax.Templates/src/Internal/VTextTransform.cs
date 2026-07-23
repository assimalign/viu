using System;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The <c>v-text</c> directive transform: compiles to a <c>textContent</c> prop (wrapping non-constant
/// expressions in <c>toDisplayString</c>) and raises the child-conflict diagnostic when the element also has
/// template children. The C# port of Vue 3.5's <c>transformVText</c> (<c>@vue/compiler-dom</c>
/// <c>transforms/vText.ts</c>).
/// </summary>
internal static class VTextTransform
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
            context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVTextNoExpression, directive.Location));
        }

        if (element.Children.Count > 0)
        {
            context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVTextWithChildren, directive.Location));
            if (context.TryGetWorkingChildren(element, out var workingChildren))
            {
                workingChildren.Clear();
            }
        }

        TemplateSyntaxNode textValue;
        if (expression is null)
        {
            textValue = Ir.SimpleExpression(string.Empty, true);
        }
        else if (ConstantAnalysis.GetConstantType(expression) > ConstantType.NotConstant)
        {
            textValue = expression;
        }
        else
        {
            textValue = Ir.CallExpression(
                context.HelperString(HelperNames.ToDisplayString),
                new object[] { expression },
                directive.Location);
        }

        return new DirectiveTransformResult
        {
            Properties = new[] { Ir.ObjectProperty("textContent", textValue) },
        };
    }
}
