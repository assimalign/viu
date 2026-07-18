using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// The <c>v-slot</c> compilation: builds a component's slots object annotated with <see cref="SlotFlags"/>,
/// and tracks slot scopes so nested slots are marked dynamic. The C# port of Vue 3.5's <c>buildSlots</c> and
/// <c>trackSlotScopes</c> (<c>@vue/compiler-core</c> <c>transforms/vSlot.ts</c>).
/// See https://vuejs.org/guide/components/slots.html.
/// </summary>
internal static class VSlotTransform
{
    private static readonly Regex ElseFamilyPattern = new(@"^else(-if)?$", RegexOptions.Compiled);
    private static readonly Regex IfFamilyPattern = new(@"^(else-)?if$", RegexOptions.Compiled);

    /// <summary>
    /// A node transform that tracks <c>v-slot</c> depth so a slot inside another slot or a <c>v-for</c> is
    /// forced dynamic. The C# port of <c>trackSlotScopes</c>.
    /// </summary>
    public static Action? TrackSlotScopes(TemplateSyntaxNode node, TransformContext context)
    {
        if (node is ElementNode { ElementType: ElementType.Component or ElementType.Template } element &&
            TransformUtilities.FindDirective(element, "slot") is not null)
        {
            context.ScopeVSlot++;
            return () => context.ScopeVSlot--;
        }

        return null;
    }

    /// <summary>Compiles a component's slots into a slots object (upstream <c>buildSlots</c>).</summary>
    /// <param name="element">The component element.</param>
    /// <param name="context">The active transform context.</param>
    /// <param name="children">The component's transformed working children.</param>
    public static (TemplateSyntaxNode Slots, bool HasDynamicSlots) BuildSlots(
        ElementNode element,
        TransformContext context,
        IReadOnlyList<TemplateSyntaxNode> children)
    {
        context.Helper(HelperNames.WithCtx);

        var slotsProperties = new List<Property>();
        var dynamicSlots = new List<TemplateSyntaxNode>();
        var hasDynamicSlots = context.ScopeVSlot > 0 || context.ScopeVFor > 0;

        // 1. Slot props on the component itself: <Comp v-slot="{ prop }"/>.
        var onComponentSlot = TransformUtilities.FindDirective(element, "slot", allowEmpty: true);
        if (onComponentSlot is not null)
        {
            var argument = onComponentSlot.Argument;
            if (argument is not null && !TransformUtilities.IsStaticExpression(argument))
            {
                hasDynamicSlots = true;
            }

            slotsProperties.Add(Ir.ObjectProperty(
                argument ?? Ir.SimpleExpression("default", true),
                BuildSlotFunction(onComponentSlot.Expression, children, element.Location)));
        }

        // 2. Template slots: <template v-slot:foo="{ prop }">.
        var hasTemplateSlots = false;
        var hasNamedDefaultSlot = false;
        var implicitDefaultChildren = new List<TemplateSyntaxNode>();
        var seenSlotNames = new HashSet<string>();
        var conditionalBranchIndex = 0;

        for (var i = 0; i < children.Count; i++)
        {
            var slotElement = children[i];
            DirectiveNode? slotDirective = null;
            if (slotElement is ElementNode { ElementType: ElementType.Template } templateElement)
            {
                slotDirective = TransformUtilities.FindDirective(templateElement, "slot", allowEmpty: true);
            }

            if (slotDirective is null)
            {
                if (slotElement is not CommentNode)
                {
                    implicitDefaultChildren.Add(slotElement);
                }

                continue;
            }

            if (onComponentSlot is not null)
            {
                context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVSlotMixedSlotUsage, slotDirective.Location));
                break;
            }

            var slotTemplate = (ElementNode)slotElement;
            hasTemplateSlots = true;
            var slotChildren = context.WorkingChildrenOf(slotTemplate, slotTemplate.Children);
            var slotName = slotDirective.Argument ?? Ir.SimpleExpression("default", true);
            var slotProps = slotDirective.Expression;

            string? staticSlotName = null;
            if (TransformUtilities.IsStaticExpression(slotName))
            {
                staticSlotName = ((SimpleExpressionNode)slotName).Content;
            }
            else
            {
                hasDynamicSlots = true;
            }

            var slotForDirective = TransformUtilities.FindDirective(slotTemplate, "for");
            var slotFunction = BuildSlotFunction(slotProps, slotChildren, slotTemplate.Location);

            var slotIf = TransformUtilities.FindDirective(slotTemplate, "if");
            DirectiveNode? slotElse;
            if (slotIf is not null)
            {
                hasDynamicSlots = true;
                dynamicSlots.Add(Ir.ConditionalExpression(
                    slotIf.Expression!,
                    BuildDynamicSlot(slotName, slotFunction, conditionalBranchIndex++),
                    DefaultFallback()));
            }
            else if ((slotElse = TransformUtilities.FindDirective(slotTemplate, ElseFamilyPattern, allowEmpty: true)) is not null)
            {
                var j = i;
                TemplateSyntaxNode? previous = null;
                while (j-- > 0)
                {
                    previous = children[j];
                    if (previous is not CommentNode)
                    {
                        break;
                    }
                }

                if (previous is ElementNode { ElementType: ElementType.Template } previousTemplate &&
                    TransformUtilities.FindDirective(previousTemplate, IfFamilyPattern) is not null)
                {
                    TemplateSyntaxNode newAlternate = slotElse.Expression is not null
                        ? Ir.ConditionalExpression(
                            slotElse.Expression,
                            BuildDynamicSlot(slotName, slotFunction, conditionalBranchIndex++),
                            DefaultFallback())
                        : BuildDynamicSlot(slotName, slotFunction, conditionalBranchIndex++);
                    dynamicSlots[dynamicSlots.Count - 1] =
                        AttachToDeepestAlternate((ConditionalExpression)dynamicSlots[dynamicSlots.Count - 1], newAlternate);
                }
                else
                {
                    context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVElseNoAdjacentIf, slotElse.Location));
                }
            }
            else if (slotForDirective is not null)
            {
                hasDynamicSlots = true;
                var parseResult = slotForDirective.Expression is SimpleExpressionNode forExpression
                    ? ForExpressionParser.Parse(forExpression)
                    : null;
                if (parseResult is not null)
                {
                    dynamicSlots.Add(Ir.CallExpression(
                        context.Helper(HelperNames.RenderList),
                        new object[]
                        {
                            parseResult.Source,
                            Ir.FunctionExpression(
                                VForTransform.CreateForLoopParameters(parseResult),
                                BuildDynamicSlot(slotName, slotFunction, null),
                                newline: true),
                        }));
                }
                else
                {
                    context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVForMalformedExpression, slotForDirective.Location));
                }
            }
            else
            {
                if (staticSlotName is not null)
                {
                    if (seenSlotNames.Contains(staticSlotName))
                    {
                        context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVSlotDuplicateSlotNames, slotDirective.Location));
                        continue;
                    }

                    seenSlotNames.Add(staticSlotName);
                    if (staticSlotName == "default")
                    {
                        hasNamedDefaultSlot = true;
                    }
                }

                slotsProperties.Add(Ir.ObjectProperty(slotName, slotFunction));
            }
        }

        if (onComponentSlot is null)
        {
            if (!hasTemplateSlots)
            {
                slotsProperties.Add(Ir.ObjectProperty("default", BuildSlotFunction(null, children, element.Location)));
            }
            else if (implicitDefaultChildren.Count > 0 && HasNonWhitespaceContent(implicitDefaultChildren))
            {
                if (hasNamedDefaultSlot)
                {
                    context.ReportError(CompilerErrorFactory.Create(
                        CompilerErrorCode.XVSlotExtraneousDefaultSlotChildren, implicitDefaultChildren[0].Location));
                }
                else
                {
                    slotsProperties.Add(Ir.ObjectProperty("default", BuildSlotFunction(null, implicitDefaultChildren, element.Location)));
                }
            }
        }

        var slotFlag = hasDynamicSlots
            ? SlotFlags.Dynamic
            : HasForwardedSlots(children, context) ? SlotFlags.Forwarded : SlotFlags.Stable;

        var allProperties = new List<Property>(slotsProperties)
        {
            Ir.ObjectProperty("_", Ir.SimpleExpression(((int)slotFlag).ToString(CultureInfo.InvariantCulture), false)),
        };

        TemplateSyntaxNode slots = Ir.ObjectExpression(allProperties, element.Location);
        if (dynamicSlots.Count > 0)
        {
            slots = Ir.CallExpression(
                context.Helper(HelperNames.CreateSlots),
                new object[] { slots, Ir.ArrayExpression(ToObjectList(dynamicSlots)) });
        }

        return (slots, hasDynamicSlots);
    }

    private static FunctionExpression BuildSlotFunction(ExpressionNode? props, IReadOnlyList<TemplateSyntaxNode> children, SourceLocation loc)
        => Ir.FunctionExpression(
            props is null ? null : new object[] { props },
            TransformFreeze.FreezeChildren(children),
            newline: false,
            isSlot: true,
            children.Count > 0 ? children[0].Location : loc);

    private static ObjectExpression BuildDynamicSlot(ExpressionNode name, FunctionExpression fn, int? index)
    {
        var properties = new List<Property>
        {
            Ir.ObjectProperty("name", name),
            Ir.ObjectProperty("fn", fn),
        };

        if (index is not null)
        {
            properties.Add(Ir.ObjectProperty("key", Ir.SimpleExpression(index.Value.ToString(CultureInfo.InvariantCulture), true)));
        }

        return Ir.ObjectExpression(properties);
    }

    private static ConditionalExpression AttachToDeepestAlternate(ConditionalExpression chain, TemplateSyntaxNode newAlternate)
    {
        if (chain.Alternate is ConditionalExpression inner)
        {
            return chain with { Alternate = AttachToDeepestAlternate(inner, newAlternate) };
        }

        return chain with { Alternate = newAlternate };
    }

    private static bool HasForwardedSlots(IReadOnlyList<TemplateSyntaxNode> children, TransformContext context)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case ElementNode { ElementType: ElementType.Slot }:
                    return true;
                case ElementNode element:
                    var elementChildren = context.TryGetWorkingChildren(element, out var working)
                        ? (IReadOnlyList<TemplateSyntaxNode>)working
                        : element.Children;
                    if (HasForwardedSlots(elementChildren, context))
                    {
                        return true;
                    }

                    break;
                case WorkingIf workingIf:
                    foreach (var branch in workingIf.Branches)
                    {
                        if (HasForwardedSlots(branch.Children, context))
                        {
                            return true;
                        }
                    }

                    break;
                case WorkingFor workingFor:
                    if (HasForwardedSlots(workingFor.Children, context))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private static bool HasNonWhitespaceContent(IReadOnlyList<TemplateSyntaxNode> children)
    {
        foreach (var child in children)
        {
            if (IsNonWhitespace(child))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNonWhitespace(TemplateSyntaxNode node) => node switch
    {
        TextNode text => text.Content.Trim().Length > 0,
        TextCallNode textCall => IsNonWhitespace(textCall.Content),
        _ => true,
    };

    private static ExpressionNode DefaultFallback() => Ir.SimpleExpression("undefined", false);

    private static IReadOnlyList<object> ToObjectList(List<TemplateSyntaxNode> nodes)
    {
        var array = new object[nodes.Count];
        for (var index = 0; index < nodes.Count; index++)
        {
            array[index] = nodes[index];
        }

        return array;
    }
}
