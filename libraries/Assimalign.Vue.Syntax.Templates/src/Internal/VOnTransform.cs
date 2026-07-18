using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The <c>v-on</c> (with argument) directive transform. Combines Vue 3.5's base <c>transformOn</c>
/// (<c>@vue/compiler-core</c> <c>transforms/vOn.ts</c> — event-name resolution, inline-statement wrapping,
/// handler caching) with the DOM <c>transformOn</c> (<c>@vue/compiler-dom</c> <c>transforms/vOn.ts</c> —
/// key/system modifier guards via <c>withModifiers</c>/<c>withKeys</c> and <c>.once</c>/<c>.capture</c>/
/// <c>.passive</c> event-option suffixes), because Vuecs is one merged compiler project.
/// </summary>
internal static class VOnTransform
{
    private static readonly HashSet<string> EventOptionModifiers = new() { "passive", "once", "capture" };

    private static readonly HashSet<string> NonKeyModifiers = new()
    {
        "stop", "prevent", "self", "ctrl", "shift", "alt", "meta", "exact", "middle",
    };

    private static readonly HashSet<string> MaybeKeyModifiers = new() { "left", "right" };

    private static readonly HashSet<string> KeyboardEvents = new() { "onkeyup", "onkeydown", "onkeypress" };

    /// <summary>The directive transform delegate (the DOM behaviour: base transform augmented with modifiers).</summary>
    public static DirectiveTransformResult Transform(
        DirectiveNode directive,
        ElementNode element,
        TransformContext context,
        Func<DirectiveTransformResult, DirectiveTransformResult>? augmentor)
        => Base(directive, element, context, baseResult =>
        {
            var modifiers = directive.Modifiers;
            if (modifiers.Count == 0)
            {
                return baseResult;
            }

            var key = baseResult.Properties[0].Key;
            var handlerExpression = baseResult.Properties[0].Value;
            var (keyModifiers, nonKeyModifiers, eventOptionModifiers) =
                ResolveModifiers(key, modifiers, context);

            // click.right and click.middle don't actually fire; normalize the event.
            if (nonKeyModifiers.Contains("right"))
            {
                key = TransformClick(key, "onContextmenu");
            }

            if (nonKeyModifiers.Contains("middle"))
            {
                key = TransformClick(key, "onMouseup");
            }

            if (nonKeyModifiers.Count > 0)
            {
                handlerExpression = Ir.CallExpression(
                    context.Helper(HelperNames.WithModifiers),
                    new object[] { handlerExpression, StringifyStringArray(nonKeyModifiers) });
            }

            if (keyModifiers.Count > 0 &&
                (!TransformUtilities.IsStaticExpression(key) ||
                 KeyboardEvents.Contains(((SimpleExpressionNode)key).Content.ToLowerInvariant())))
            {
                handlerExpression = Ir.CallExpression(
                    context.Helper(HelperNames.WithKeys),
                    new object[] { handlerExpression, StringifyStringArray(keyModifiers) });
            }

            if (eventOptionModifiers.Count > 0)
            {
                var postfix = new StringBuilder();
                foreach (var modifier in eventOptionModifiers)
                {
                    postfix.Append(CompilerText.Capitalize(modifier));
                }

                key = TransformUtilities.IsStaticExpression(key)
                    ? Ir.SimpleExpression(((SimpleExpressionNode)key).Content + postfix, true)
                    : Ir.CompoundExpression("(", key, ") + \"" + postfix + "\"");
            }

            return new DirectiveTransformResult { Properties = new[] { Ir.ObjectProperty((ExpressionNode)key, handlerExpression) } };
        });

    // Port of the target-agnostic base transformOn.
    private static DirectiveTransformResult Base(
        DirectiveNode directive,
        ElementNode element,
        TransformContext context,
        Func<DirectiveTransformResult, DirectiveTransformResult> augmentor)
    {
        var modifiers = directive.Modifiers;
        if (directive.Expression is null && modifiers.Count == 0)
        {
            context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVOnNoExpression, directive.Location));
        }

        var eventName = BuildEventName(directive.Argument!, element, context);

        var expression = directive.Expression as SimpleExpressionNode;
        if (expression is not null && expression.Content.Trim().Length == 0)
        {
            expression = null;
        }

        var shouldCache = context.CacheHandlers && expression is null && !context.InVOnce;
        TemplateSyntaxNode? handler = expression;
        if (expression is not null)
        {
            var isMemberExpression = ExpressionShape.IsMemberExpression(expression);
            var isInlineStatement = !(isMemberExpression || ExpressionShape.IsFunctionExpression(expression));
            var hasMultipleStatements = expression.Content.IndexOf(';') >= 0;

            // Rewrite the handler's identifiers, with the event variable in scope for an inline
            // statement so its assignments unwrap refs (upstream transformOn: addIdentifiers($event)
            // around processExpression). `$event` is not a legal C# identifier, so under prefixing the
            // inline statement is parsed against the Vuecs spelling `__event` — the parameter name the
            // wrapping lambda emits below. Template authors keep Vue's `$event`; the substitution is
            // the documented C# divergence (docs/DESIGN.md).
            ExpressionNode processedExpression = expression;
            if (context.PrefixIdentifiers)
            {
                if (isInlineStatement)
                {
                    if (expression.Content.Contains("$event"))
                    {
                        expression = expression with { Content = expression.Content.Replace("$event", "__event") };
                    }

                    context.AddIdentifiers("__event");
                }

                processedExpression = ExpressionProcessor.ProcessExpression(expression, context, asRawStatements: hasMultipleStatements);

                if (isInlineStatement)
                {
                    context.RemoveIdentifiers("__event");
                }
            }

            handler = processedExpression;
            if (isInlineStatement || (shouldCache && isMemberExpression))
            {
                handler = Ir.CompoundExpression(
                    (isInlineStatement ? (context.PrefixIdentifiers ? "__event" : "$event") : "(...args)") + " => " + (hasMultipleStatements ? "{" : "("),
                    processedExpression,
                    hasMultipleStatements ? "}" : ")");
            }
        }

        handler ??= Ir.SimpleExpression("() => {}", false, directive.Location);

        var result = new DirectiveTransformResult { Properties = new[] { Ir.ObjectProperty(eventName, handler) } };
        result = augmentor(result);

        if (shouldCache)
        {
            var property = result.Properties[0];
            result = result with { Properties = new[] { property with { Value = context.Cache(property.Value) } } };
        }

        // Mark keys as handler keys so prop normalization ignores dynamic handler keys.
        var marked = new Property[result.Properties.Count];
        for (var index = 0; index < result.Properties.Count; index++)
        {
            var property = result.Properties[index];
            marked[index] = property with { Key = MarkHandlerKey(property.Key) };
        }

        return result with { Properties = marked };
    }

    private static ExpressionNode BuildEventName(ExpressionNode argument, ElementNode element, TransformContext context)
    {
        if (argument is SimpleExpressionNode simple)
        {
            if (simple.IsStatic)
            {
                var rawName = simple.Content;
                if (rawName.StartsWith("vue:", StringComparison.Ordinal))
                {
                    rawName = "vnode-" + rawName.Substring(4);
                }

                var eventString =
                    element.ElementType != ElementType.Element ||
                    rawName.StartsWith("vnode", StringComparison.Ordinal) ||
                    !HasUppercase(rawName)
                        ? CompilerText.ToHandlerKey(CompilerText.Camelize(rawName))
                        : "on:" + rawName;
                return Ir.SimpleExpression(eventString, true, simple.Location);
            }

            return Ir.CompoundExpression($"{context.HelperString(HelperNames.ToHandlerKey)}(", argument, ")");
        }

        var compound = (CompoundExpressionNode)argument;
        var parts = new object[compound.Parts.Count + 2];
        parts[0] = $"{context.HelperString(HelperNames.ToHandlerKey)}(";
        for (var index = 0; index < compound.Parts.Count; index++)
        {
            parts[index + 1] = compound.Parts[index];
        }

        parts[parts.Length - 1] = ")";
        return compound with { Parts = new SyntaxList<object>(parts) };
    }

    private static (List<string> Key, List<string> NonKey, List<string> EventOption) ResolveModifiers(
        ExpressionNode key,
        SyntaxList<SimpleExpressionNode> modifiers,
        TransformContext context)
    {
        var keyModifiers = new List<string>();
        var nonKeyModifiers = new List<string>();
        var eventOptionModifiers = new List<string>();

        foreach (var entry in modifiers)
        {
            var modifier = entry.Content;
            if (EventOptionModifiers.Contains(modifier))
            {
                eventOptionModifiers.Add(modifier);
            }
            else if (MaybeKeyModifiers.Contains(modifier))
            {
                if (TransformUtilities.IsStaticExpression(key))
                {
                    if (KeyboardEvents.Contains(((SimpleExpressionNode)key).Content.ToLowerInvariant()))
                    {
                        keyModifiers.Add(modifier);
                    }
                    else
                    {
                        nonKeyModifiers.Add(modifier);
                    }
                }
                else
                {
                    keyModifiers.Add(modifier);
                    nonKeyModifiers.Add(modifier);
                }
            }
            else if (NonKeyModifiers.Contains(modifier))
            {
                nonKeyModifiers.Add(modifier);
            }
            else
            {
                keyModifiers.Add(modifier);
            }
        }

        return (keyModifiers, nonKeyModifiers, eventOptionModifiers);
    }

    private static ExpressionNode TransformClick(ExpressionNode key, string @event)
    {
        var isStaticClick = TransformUtilities.IsStaticExpression(key) &&
                            ((SimpleExpressionNode)key).Content.ToLowerInvariant() == "onclick";
        if (isStaticClick)
        {
            return Ir.SimpleExpression(@event, true);
        }

        if (key is SimpleExpressionNode)
        {
            return key;
        }

        return Ir.CompoundExpression("(", key, $") === \"onClick\" ? \"{@event}\" : (", key, ")");
    }

    private static ExpressionNode MarkHandlerKey(ExpressionNode key) => key switch
    {
        SimpleExpressionNode simple => simple with { IsHandlerKey = true },
        CompoundExpressionNode compound => compound with { IsHandlerKey = true },
        _ => key,
    };

    private static bool HasUppercase(string value)
    {
        foreach (var character in value)
        {
            if (character >= 'A' && character <= 'Z')
            {
                return true;
            }
        }

        return false;
    }

    // JSON.stringify of a string array, e.g. ["stop","prevent"].
    private static string StringifyStringArray(IReadOnlyList<string> values)
    {
        var builder = new StringBuilder("[");
        for (var index = 0; index < values.Count; index++)
        {
            builder.Append('"').Append(values[index]).Append('"');
            if (index < values.Count - 1)
            {
                builder.Append(',');
            }
        }

        return builder.Append(']').ToString();
    }
}
