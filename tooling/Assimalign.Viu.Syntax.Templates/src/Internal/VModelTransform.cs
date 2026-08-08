using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The <c>v-model</c> directive transform. It does two jobs in one pass: the platform-neutral half emits
/// the <c>modelValue</c> prop, the <c>onUpdate:modelValue</c> handler, and — on a component — the
/// modifiers object; the DOM half selects the runtime model directive by element and input type. They
/// are one transform because the DOM half needs the element classification the neutral half computes.
/// </summary>
internal static class VModelTransform
{
    /// <summary>The directive transform delegate (DOM behaviour layered over the base transform).</summary>
    public static DirectiveTransformResult Transform(
        DirectiveNode directive,
        ElementNode element,
        TransformContext context,
        Func<DirectiveTransformResult, DirectiveTransformResult>? augmentor)
    {
        var baseResult = Base(directive, element, context);

        // Base errored OR this is a component v-model (props are enough).
        if (baseResult.Properties.Count == 0 || element.ElementType == ElementType.Component)
        {
            return baseResult;
        }

        if (directive.Argument is not null)
        {
            context.ReportError(
                CompilerErrorFactory.Create(CompilerErrorCode.XVModelArgumentOnElement, directive.Argument.Location));
        }

        var tag = element.Tag;
        var isCustomElement = context.IsCustomElement(tag);
        var runtimeDirective = HelperNames.VModelText;
        var isInvalidType = false;

        if (tag is "input" or "textarea" or "select" || isCustomElement)
        {
            if (tag == "input" || isCustomElement)
            {
                var type = TransformUtilities.FindProperty(element, "type");
                if (type is DirectiveNode)
                {
                    runtimeDirective = HelperNames.VModelDynamic;
                }
                else if (type is AttributeNode { Value: { } typeValue })
                {
                    switch (typeValue.Content)
                    {
                        case "radio":
                            runtimeDirective = HelperNames.VModelRadio;
                            break;
                        case "checkbox":
                            runtimeDirective = HelperNames.VModelCheckbox;
                            break;
                        case "file":
                            isInvalidType = true;
                            context.ReportError(CompilerErrorFactory.Create(
                                CompilerErrorCode.XVModelOnFileInputElement, directive.Location));
                            break;
                        default:
                            CheckDuplicatedValue(element, context);
                            break;
                    }
                }
                else if (TransformUtilities.HasDynamicKeyVBind(element))
                {
                    runtimeDirective = HelperNames.VModelDynamic;
                }
                else
                {
                    CheckDuplicatedValue(element, context);
                }
            }
            else if (tag == "select")
            {
                runtimeDirective = HelperNames.VModelSelect;
            }
            else
            {
                CheckDuplicatedValue(element, context);
            }

            if (!isInvalidType)
            {
                baseResult = baseResult with { NeedRuntime = context.Helper(runtimeDirective) };
            }
        }
        else
        {
            context.ReportError(
                CompilerErrorFactory.Create(CompilerErrorCode.XVModelOnInvalidElement, directive.Location));
        }

        // Native v-model doesn't need the modelValue prop — it is passed as binding.value.
        var filtered = new List<ObjectProperty>(baseResult.Properties.Count);
        foreach (var property in baseResult.Properties)
        {
            if (property.Key is SimpleExpressionNode { Content: "modelValue" })
            {
                continue;
            }

            filtered.Add(property);
        }

        return baseResult with { Properties = filtered };
    }

    // Port of the target-agnostic base transformModel.
    private static DirectiveTransformResult Base(DirectiveNode directive, ElementNode element, TransformContext context)
    {
        var expression = directive.Expression;
        var argument = directive.Argument;
        if (expression is null)
        {
            context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVModelNoExpression, directive.Location));
            return Empty();
        }

        var rawExpression = expression.Location.Source.Trim();
        var expressionString = expression is SimpleExpressionNode simple ? simple.Content : rawExpression;

        if (expressionString.Trim().Length == 0 || !ExpressionShape.IsMemberExpression(expression))
        {
            context.ReportError(
                CompilerErrorFactory.Create(CompilerErrorCode.XVModelMalformedExpression, expression.Location));
            return Empty();
        }

        var propertyName = argument ?? Ir.SimpleExpression("modelValue", true);
        var eventName = BuildEventName(argument);

        // eventArg is "$event" in the opaque (non-TS) build.
        var assignment = CreateAssignment(expression);

        var properties = new List<ObjectProperty>
        {
            Ir.ObjectProperty(propertyName, directive.Expression!),
            Ir.ObjectProperty(eventName, assignment),
        };

        // modelModifiers: { foo: true } — only emitted for component v-model.
        if (directive.Modifiers.Count > 0 && element.ElementType == ElementType.Component)
        {
            var modifiers = new StringBuilder();
            for (var index = 0; index < directive.Modifiers.Count; index++)
            {
                var modifier = directive.Modifiers[index].Content;
                if (index > 0)
                {
                    modifiers.Append(", ");
                }

                modifiers.Append(CompilerText.IsSimpleIdentifier(modifier) ? modifier : JsonString(modifier)).Append(": true");
            }

            var modifiersKey = BuildModifiersKey(argument);
            properties.Add(Ir.ObjectProperty(
                modifiersKey,
                Ir.SimpleExpression($"{{ {modifiers} }}", false, directive.Location, ConstantType.CanCache)));
        }

        return new DirectiveTransformResult { Properties = properties };
    }

    /// <summary>
    /// Builds the write-back lambda shared by the native directive carrier and the
    /// <c>onUpdate:modelValue</c> property.
    /// </summary>
    /// <param name="expression">The writable model expression.</param>
    /// <returns>The generated assignment lambda.</returns>
    internal static CompoundExpressionNode CreateAssignment(ExpressionNode expression)
        => Ir.CompoundExpression("$event => ((", expression, ") = $event)");

    /// <summary>
    /// Builds the statement-bodied assignment lambda required by <c>Action&lt;object?&gt;</c>. The
    /// expression-bodied form is retained for the virtual node property because it may bind as a
    /// value-returning handler; a parenthesized assignment is not a valid C# statement expression.
    /// </summary>
    /// <param name="expression">The writable model expression.</param>
    /// <returns>The generated assignment action.</returns>
    private static CompoundExpressionNode CreateAssignmentAction(ExpressionNode expression)
        => Ir.CompoundExpression("$event => { ", expression, " = $event; }");

    /// <summary>
    /// Builds the Browser runtime carrier for a native <c>v-model</c>. Viu cannot recover a setter
    /// from a virtual node property through reflection, so generated code carries the current value
    /// and its assignment lambda together. Specified by <c>[SFC-CG-7]</c>.
    /// </summary>
    /// <param name="expression">The writable model expression.</param>
    /// <param name="modifiers">The compile-time modifier names carried by the typed binding.</param>
    /// <returns>The generated <c>ViuModelBinding</c> construction.</returns>
    internal static CompoundExpressionNode CreateNativeBinding(
        ExpressionNode expression,
        IReadOnlyList<SimpleExpressionNode> modifiers)
    {
        if (modifiers.Count == 0)
        {
            return Ir.CompoundExpression(
                "new global::Assimalign.Viu.Browser.ViuModelBinding(",
                expression,
                ", ",
                CreateAssignmentAction(expression),
                ")");
        }

        var parts = new List<object>
        {
            "new global::Assimalign.Viu.Browser.ViuModelBinding(",
            expression,
            ", ",
            CreateAssignmentAction(expression),
            ", new string[] { ",
        };
        for (var index = 0; index < modifiers.Count; index++)
        {
            if (index > 0)
            {
                parts.Add(", ");
            }

            parts.Add(JsonString(modifiers[index].Content));
        }

        parts.Add(" })");
        return Ir.CompoundExpression(parts.ToArray());
    }

    private static ExpressionNode BuildEventName(ExpressionNode? argument)
    {
        if (argument is null)
        {
            return Ir.SimpleExpression("onUpdate:modelValue", true);
        }

        return TransformUtilities.IsStaticExpression(argument)
            ? Ir.SimpleExpression("onUpdate:" + CompilerText.Camelize(((SimpleExpressionNode)argument).Content), true)
            : Ir.CompoundExpression("\"onUpdate:\" + ", argument);
    }

    private static ExpressionNode BuildModifiersKey(ExpressionNode? argument)
    {
        if (argument is null)
        {
            return Ir.SimpleExpression("modelModifiers", true);
        }

        return TransformUtilities.IsStaticExpression(argument)
            ? Ir.SimpleExpression(((SimpleExpressionNode)argument).Content + "Modifiers", true)
            : Ir.CompoundExpression(argument, " + \"Modifiers\"");
    }

    private static void CheckDuplicatedValue(ElementNode element, TransformContext context)
    {
        var value = TransformUtilities.FindDirective(element, "bind");
        if (value is not null && TransformUtilities.IsStaticArgumentOf(value.Argument, "value"))
        {
            context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVModelUnnecessaryValue, value.Location));
        }
    }

    private static DirectiveTransformResult Empty() => new() { Properties = Array.Empty<ObjectProperty>() };

    private static string JsonString(string value) => "\"" + value + "\"";
}
