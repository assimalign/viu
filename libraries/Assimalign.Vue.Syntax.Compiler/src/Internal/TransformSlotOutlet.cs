using System;
using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// The slot-outlet transform: compiles a <c>&lt;slot&gt;</c> element to a <c>renderSlot</c> call carrying the
/// slot name, forwarded props, and fallback content. The C# port of Vue 3.5's <c>transformSlotOutlet</c> and
/// <c>processSlotOutlet</c> (<c>@vue/compiler-core</c> <c>transforms/transformSlotOutlet.ts</c>).
/// </summary>
internal static class TransformSlotOutlet
{
    /// <summary>The node transform (builds the render-slot call on exit, after fallback children are transformed).</summary>
    public static Action? Transform(TemplateSyntaxNode node, TransformContext context)
    {
        if (node is not ElementNode { ElementType: ElementType.Slot } element)
        {
            return null;
        }

        return () =>
        {
            var (slotName, slotProps) = ProcessSlotOutlet(element, context);
            var slotChildren = context.WorkingChildrenOf(element, element.Children);

            var slotArguments = new List<object> { "$slots", slotName, "{}", "undefined", "true" };
            var expectedLength = 2;

            if (slotProps is not null)
            {
                slotArguments[2] = slotProps;
                expectedLength = 3;
            }

            if (slotChildren.Count > 0)
            {
                slotArguments[3] = Ir.FunctionExpression(
                    Array.Empty<object>(),
                    TransformFreeze.FreezeChildren(slotChildren),
                    newline: false,
                    isSlot: false,
                    element.Location);
                expectedLength = 4;
            }

            if (context.ScopeId is not null && !context.Slotted)
            {
                expectedLength = 5;
            }

            slotArguments.RemoveRange(expectedLength, slotArguments.Count - expectedLength);
            context.SetCodegenNode(element, Ir.CallExpression(context.Helper(HelperNames.RenderSlot), slotArguments, element.Location));
        };
    }

    private static (object SlotName, TemplateSyntaxNode? SlotProps) ProcessSlotOutlet(ElementNode element, TransformContext context)
    {
        object slotName = "\"default\"";
        TemplateSyntaxNode? slotProps = null;
        var nonNameProperties = new List<PropertyNode>();

        foreach (var property in element.Properties)
        {
            if (property is AttributeNode attribute)
            {
                if (attribute.Value is not null)
                {
                    if (attribute.Name == "name")
                    {
                        slotName = "\"" + attribute.Value.Content + "\"";
                    }
                    else
                    {
                        nonNameProperties.Add(attribute with { Name = CompilerText.Camelize(attribute.Name) });
                    }
                }
            }
            else if (property is DirectiveNode directive)
            {
                if (directive.Name == "bind" && TransformUtilities.IsStaticArgumentOf(directive.Argument, "name"))
                {
                    if (directive.Expression is not null)
                    {
                        slotName = directive.Expression;
                    }
                    else if (directive.Argument is SimpleExpressionNode argument)
                    {
                        slotName = Ir.SimpleExpression(CompilerText.Camelize(argument.Content), false, argument.Location);
                    }
                }
                else
                {
                    if (directive.Name == "bind" && directive.Argument is SimpleExpressionNode { IsStatic: true } bindArgument)
                    {
                        directive = directive with { Argument = bindArgument with { Content = CompilerText.Camelize(bindArgument.Content) } };
                    }

                    nonNameProperties.Add(directive);
                }
            }
        }

        if (nonNameProperties.Count > 0)
        {
            var built = TransformElement.BuildProps(
                element,
                context,
                new SyntaxList<PropertyNode>(nonNameProperties.ToArray()),
                isComponent: false,
                isDynamicComponent: false);
            slotProps = built.Props;

            if (built.Directives.Count > 0)
            {
                context.ReportError(CompilerErrorFactory.Create(
                    CompilerErrorCode.XVSlotUnexpectedDirectiveOnSlotOutlet, built.Directives[0].Location));
            }
        }

        return (slotName, slotProps);
    }
}
