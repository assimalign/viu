using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The expression node transform: rewrites the identifiers inside interpolations and element directive
/// expressions and arguments against the template scope and binding metadata. The C# port of Vue 3.5's
/// <c>transformExpression</c> (<c>@vue/compiler-core</c> <c>transforms/transformExpression.ts</c>).
/// </summary>
/// <remarks>
/// Only installed in the pipeline when <see cref="TransformOptions.PrefixIdentifiers"/> is set, mirroring
/// upstream's <c>!__BROWSER__ &amp;&amp; prefixIdentifiers</c> gate; the default opaque-expression pipeline is
/// unchanged. Like upstream it skips <c>v-for</c> (its source and aliases are handled by
/// <see cref="VForTransform"/>) and the expression of a <c>v-on</c> that has an argument (handled by
/// <see cref="VOnTransform"/>); <c>v-slot</c> expressions are validated as parameter declarations.
/// </remarks>
internal static class TransformExpression
{
    /// <summary>The node transform (work happens on entry, before children are traversed).</summary>
    public static Action? Transform(TemplateSyntaxNode node, TransformContext context)
    {
        switch (node)
        {
            case InterpolationNode interpolation when interpolation.Content is SimpleExpressionNode content:
                var processed = ExpressionProcessor.ProcessExpression(content, context);
                if (!ReferenceEquals(processed, content))
                {
                    context.ReplaceNode(interpolation with { Content = processed });
                }

                break;
            case ElementNode element:
                RewriteElementDirectives(element, context);
                break;
        }

        return null;
    }

    private static void RewriteElementDirectives(ElementNode element, TransformContext context)
    {
        var properties = element.Properties;
        List<PropertyNode>? rewritten = null;

        for (var index = 0; index < properties.Count; index++)
        {
            var property = properties[index];
            if (property is not DirectiveNode directive || directive.Name == "for")
            {
                continue;
            }

            var newDirective = directive;

            // Rewrite the expression, except a v-on with an argument (its handler is processed by transformOn).
            if (directive.Expression is SimpleExpressionNode expression &&
                !(directive.Name == "on" && directive.Argument is not null))
            {
                var processedExpression = ExpressionProcessor.ProcessExpression(
                    expression,
                    context,
                    asParams: directive.Name == "slot");
                if (!ReferenceEquals(processedExpression, expression))
                {
                    newDirective = newDirective with { Expression = processedExpression };
                }
            }

            // Rewrite a dynamic argument (upstream processes non-static SIMPLE_EXPRESSION args).
            if (directive.Argument is SimpleExpressionNode { IsStatic: false } argument)
            {
                var processedArgument = ExpressionProcessor.ProcessExpression(argument, context);
                if (!ReferenceEquals(processedArgument, argument))
                {
                    newDirective = newDirective with { Argument = processedArgument };
                }
            }

            if (!ReferenceEquals(newDirective, directive))
            {
                rewritten ??= NewPropertyList(properties);
                rewritten[index] = newDirective;
            }
        }

        if (rewritten is not null)
        {
            context.ReplaceNode(element with { Properties = new SyntaxList<PropertyNode>(rewritten.ToArray()) });
        }
    }

    private static List<PropertyNode> NewPropertyList(SyntaxList<PropertyNode> properties)
    {
        var list = new List<PropertyNode>(properties.Count);
        foreach (var property in properties)
        {
            list.Add(property);
        }

        return list;
    }
}
