using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// Shared query and rewrite helpers over the AST used across the transforms, ported from Vue 3.5's
/// <c>@vue/compiler-core</c> <c>utils.ts</c> (<c>findDir</c>, <c>findProp</c>, <c>isStaticExp</c>,
/// <c>injectProp</c>, <c>getMemoedVNodeCall</c>, …) and <c>ast.ts</c> (<c>convertToBlock</c>).
/// </summary>
internal static class TransformUtilities
{
    /// <summary>Whether <paramref name="node"/> is a static simple expression (upstream <c>isStaticExp</c>).</summary>
    public static bool IsStaticExpression(TemplateSyntaxNode? node)
        => node is SimpleExpressionNode { IsStatic: true };

    /// <summary>Whether <paramref name="argument"/> is the static argument <paramref name="name"/> (upstream <c>isStaticArgOf</c>).</summary>
    public static bool IsStaticArgumentOf(ExpressionNode? argument, string name)
        => argument is SimpleExpressionNode { IsStatic: true } simple && simple.Content == name;

    /// <summary>Finds a directive by name (upstream <c>findDir</c>).</summary>
    public static DirectiveNode? FindDirective(ElementNode element, string name, bool allowEmpty = false)
    {
        foreach (var property in element.Properties)
        {
            if (property is DirectiveNode directive && (allowEmpty || directive.Expression is not null) &&
                directive.Name == name)
            {
                return directive;
            }
        }

        return null;
    }

    /// <summary>Finds a directive whose name matches <paramref name="pattern"/> (upstream <c>findDir</c> regex form).</summary>
    public static DirectiveNode? FindDirective(ElementNode element, Regex pattern, bool allowEmpty = false)
    {
        foreach (var property in element.Properties)
        {
            if (property is DirectiveNode directive && (allowEmpty || directive.Expression is not null) &&
                pattern.IsMatch(directive.Name))
            {
                return directive;
            }
        }

        return null;
    }

    /// <summary>Finds a plain attribute or static <c>v-bind</c> by name (upstream <c>findProp</c>).</summary>
    public static PropertyNode? FindProperty(ElementNode element, string name, bool dynamicOnly = false, bool allowEmpty = false)
    {
        foreach (var property in element.Properties)
        {
            if (property is AttributeNode attribute)
            {
                if (dynamicOnly)
                {
                    continue;
                }

                if (attribute.Name == name && (attribute.Value is not null || allowEmpty))
                {
                    return attribute;
                }
            }
            else if (property is DirectiveNode { Name: "bind" } directive &&
                     (directive.Expression is not null || allowEmpty) &&
                     IsStaticArgumentOf(directive.Argument, name))
            {
                return directive;
            }
        }

        return null;
    }

    /// <summary>Whether the element has a <c>v-bind</c> with a dynamic key (upstream <c>hasDynamicKeyVBind</c>).</summary>
    public static bool HasDynamicKeyVBind(ElementNode element)
    {
        foreach (var property in element.Properties)
        {
            if (property is DirectiveNode { Name: "bind" } directive &&
                (directive.Argument is null ||
                 directive.Argument is not SimpleExpressionNode ||
                 directive.Argument is SimpleExpressionNode { IsStatic: false }))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="node"/> is a <c>&lt;template&gt;</c> container (upstream <c>isTemplateNode</c>).</summary>
    public static bool IsTemplateNode(TemplateSyntaxNode node)
        => node is ElementNode { ElementType: ElementType.Template };

    /// <summary>Whether <paramref name="node"/> is a <c>&lt;slot&gt;</c> outlet (upstream <c>isSlotOutlet</c>).</summary>
    public static bool IsSlotOutlet(TemplateSyntaxNode node)
        => node is ElementNode { ElementType: ElementType.Slot };

    /// <summary>Unwraps a <c>withMemo(...)</c>-wrapped vnode call (upstream <c>getMemoedVNodeCall</c>).</summary>
    public static TemplateSyntaxNode GetMemoedVNodeCall(TemplateSyntaxNode node)
    {
        if (node is CallExpression { Callee: RuntimeHelper helper } call &&
            helper == HelperNames.WithMemo &&
            call.Arguments.Count > 1 &&
            call.Arguments[1] is FunctionExpression { Returns: TemplateSyntaxNode returns })
        {
            return returns;
        }

        return node;
    }

    /// <summary>Turns a vnode call into a block, registering the block helpers (upstream <c>convertToBlock</c>).</summary>
    public static VNodeCall ConvertToBlock(VNodeCall node, TransformContext context)
    {
        if (node.IsBlock)
        {
            return node;
        }

        context.RemoveHelper(TransformContext.GetVNodeHelper(context.InSSR, node.IsComponent));
        context.Helper(HelperNames.OpenBlock);
        context.Helper(TransformContext.GetVNodeBlockHelper(context.InSSR, node.IsComponent));
        return node with { IsBlock = true };
    }

    /// <summary>
    /// Injects <paramref name="property"/> as the first prop of a vnode call or <c>renderSlot</c> call
    /// (upstream <c>injectProp</c>), returning the rewritten node.
    /// </summary>
    public static TemplateSyntaxNode InjectProperty(TemplateSyntaxNode node, Property property, TransformContext context)
    {
        var isVNodeCall = node is VNodeCall;
        var properties = isVNodeCall
            ? ((VNodeCall)node).Props
            : ((CallExpression)node).Arguments.Count > 2 ? ((CallExpression)node).Arguments[2] as TemplateSyntaxNode : null;

        TemplateSyntaxNode? propertiesWithInjection;
        if (properties is null)
        {
            propertiesWithInjection = Ir.ObjectExpression(new[] { property });
        }
        else if (properties is CallExpression callProperties)
        {
            // merged props via mergeProps(...): inject into the first object-literal arg if present.
            var first = callProperties.Arguments.Count > 0 ? callProperties.Arguments[0] : null;
            if (first is ObjectExpression firstObject)
            {
                if (!HasProperty(property, firstObject))
                {
                    propertiesWithInjection = callProperties with
                    {
                        Arguments = ReplaceAt(callProperties.Arguments, 0, Unshift(firstObject, property)),
                    };
                }
                else
                {
                    propertiesWithInjection = callProperties;
                }
            }
            else if (callProperties.Callee is RuntimeHelper toHandlers && toHandlers == HelperNames.ToHandlers)
            {
                propertiesWithInjection = Ir.CallExpression(
                    context.Helper(HelperNames.MergeProps),
                    new object[] { Ir.ObjectExpression(new[] { property }), callProperties });
            }
            else
            {
                propertiesWithInjection = callProperties with
                {
                    Arguments = Prepend(callProperties.Arguments, Ir.ObjectExpression(new[] { property })),
                };
            }
        }
        else if (properties is ObjectExpression objectProperties)
        {
            propertiesWithInjection = HasProperty(property, objectProperties)
                ? objectProperties
                : Unshift(objectProperties, property);
        }
        else
        {
            // single v-bind expression: merge.
            propertiesWithInjection = Ir.CallExpression(
                context.Helper(HelperNames.MergeProps),
                new object[] { Ir.ObjectExpression(new[] { property }), properties });
        }

        if (isVNodeCall)
        {
            return ((VNodeCall)node) with { Props = propertiesWithInjection };
        }

        var arguments = new List<object>(((CallExpression)node).Arguments);
        while (arguments.Count <= 2)
        {
            arguments.Add("{}");
        }

        arguments[2] = propertiesWithInjection!;
        return ((CallExpression)node) with { Arguments = new SyntaxList<object>(arguments.ToArray()) };
    }

    private static ObjectExpression Unshift(ObjectExpression target, Property property)
    {
        var properties = new List<Property>(target.Properties.Count + 1) { property };
        properties.AddRange(target.Properties);
        return target with { Properties = new SyntaxList<Property>(properties.ToArray()) };
    }

    private static SyntaxList<object> ReplaceAt(SyntaxList<object> arguments, int index, object value)
    {
        var array = new object[arguments.Count];
        for (var i = 0; i < arguments.Count; i++)
        {
            array[i] = i == index ? value : arguments[i];
        }

        return new SyntaxList<object>(array);
    }

    private static SyntaxList<object> Prepend(SyntaxList<object> arguments, object value)
    {
        var array = new object[arguments.Count + 1];
        array[0] = value;
        for (var i = 0; i < arguments.Count; i++)
        {
            array[i + 1] = arguments[i];
        }

        return new SyntaxList<object>(array);
    }

    private static bool HasProperty(Property property, ObjectExpression target)
    {
        if (property.Key is not SimpleExpressionNode key)
        {
            return false;
        }

        foreach (var existing in target.Properties)
        {
            if (existing.Key is SimpleExpressionNode existingKey && existingKey.Content == key.Content)
            {
                return true;
            }
        }

        return false;
    }
}
