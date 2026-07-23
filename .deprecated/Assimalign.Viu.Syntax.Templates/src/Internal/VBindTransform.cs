using System;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The <c>v-bind</c> (with argument) directive transform: emits a single prop and applies the
/// <c>.camel</c>/<c>.prop</c>/<c>.attr</c> modifiers and the same-name shorthand. The C# port of Vue 3.5's
/// <c>transformBind</c> (<c>@vue/compiler-core</c> <c>transforms/vBind.ts</c>). Argument-less <c>v-bind</c>
/// (<c>v-bind="object"</c>) is handled by the element transform.
/// </summary>
internal static class VBindTransform
{
    /// <summary>The directive transform delegate.</summary>
    public static DirectiveTransformResult Transform(
        DirectiveNode directive,
        ElementNode element,
        TransformContext context,
        Func<DirectiveTransformResult, DirectiveTransformResult>? augmentor)
    {
        var argument = directive.Argument!;
        var expression = directive.Expression;

        // Empty expression (e.g. `:foo=""`). Non-browser semantics: report and emit an empty prop.
        if (expression is SimpleExpressionNode { IsStatic: false } simpleEmpty && simpleEmpty.Content.Trim().Length == 0)
        {
            context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVBindNoExpression, directive.Location));
            return new DirectiveTransformResult
            {
                Properties = new[] { Ir.ObjectProperty(argument, Ir.SimpleExpression(string.Empty, true, directive.Location)) },
            };
        }

        // Same-name shorthand: `:arg` expands to `:arg="arg"`.
        if (expression is null)
        {
            if (argument is not SimpleExpressionNode { IsStatic: true })
            {
                context.ReportError(
                    CompilerErrorFactory.Create(CompilerErrorCode.XVBindInvalidSameNameArgument, argument.Location));
                return new DirectiveTransformResult
                {
                    Properties = new[] { Ir.ObjectProperty(argument, Ir.SimpleExpression(string.Empty, true, directive.Location)) },
                };
            }

            expression = CreateShorthandExpression((SimpleExpressionNode)argument);
        }

        // Null-guard a dynamic argument.
        if (argument is not SimpleExpressionNode simpleArgument)
        {
            argument = WrapCompound((CompoundExpressionNode)argument, "(", ") || \"\"");
        }
        else if (!simpleArgument.IsStatic)
        {
            argument = simpleArgument with { Content = simpleArgument.Content + " || \"\"" };
        }

        if (HasModifier(directive, "camel"))
        {
            argument = argument is SimpleExpressionNode camelArgument
                ? camelArgument.IsStatic
                    ? camelArgument with { Content = CompilerText.Camelize(camelArgument.Content) }
                    : camelArgument with { Content = $"{context.HelperString(HelperNames.Camelize)}({camelArgument.Content})" }
                : WrapCompound((CompoundExpressionNode)argument, $"{context.HelperString(HelperNames.Camelize)}(", ")");
        }

        if (!context.InSSR)
        {
            if (HasModifier(directive, "prop"))
            {
                argument = InjectPrefix(argument, ".");
            }

            if (HasModifier(directive, "attr"))
            {
                argument = InjectPrefix(argument, "^");
            }
        }

        return new DirectiveTransformResult
        {
            Properties = new[] { Ir.ObjectProperty(argument, expression) },
        };
    }

    /// <summary>Expands the same-name <c>v-bind</c> shorthand to its camel-cased binding (upstream <c>transformBindShorthand</c>).</summary>
    public static SimpleExpressionNode CreateShorthandExpression(SimpleExpressionNode argument)
        => Ir.SimpleExpression(CompilerText.Camelize(argument.Content), false, argument.Location);

    private static bool HasModifier(DirectiveNode directive, string modifier)
    {
        foreach (var entry in directive.Modifiers)
        {
            if (entry.Content == modifier)
            {
                return true;
            }
        }

        return false;
    }

    private static ExpressionNode InjectPrefix(ExpressionNode argument, string prefix)
    {
        if (argument is SimpleExpressionNode simple)
        {
            return simple.IsStatic
                ? simple with { Content = prefix + simple.Content }
                : simple with { Content = "`" + prefix + "${" + simple.Content + "}`" };
        }

        return WrapCompound((CompoundExpressionNode)argument, $"'{prefix}' + (", ")");
    }

    private static CompoundExpressionNode WrapCompound(CompoundExpressionNode compound, string open, string close)
    {
        var parts = new object[compound.Parts.Count + 2];
        parts[0] = open;
        for (var index = 0; index < compound.Parts.Count; index++)
        {
            parts[index + 1] = compound.Parts[index];
        }

        parts[parts.Length - 1] = close;
        return compound with { Parts = new SyntaxList<object>(parts) };
    }
}
