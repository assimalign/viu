using System;
using System.Collections.Generic;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The <c>v-for</c> transform: builds a <see cref="WorkingFor"/> capturing the source and decomposed aliases
/// and compiles it to a <c>renderList</c> fragment block whose keyed/unkeyed/stable classification drives the
/// runtime diff. The C# port of Vue 3.5's <c>transformFor</c> and <c>processFor</c>
/// (<c>@vue/compiler-core</c> <c>transforms/vFor.ts</c>). See https://vuejs.org/guide/essentials/list.html.
/// </summary>
internal static class VForTransform
{
    /// <summary>The node transform (built via the structural directive factory).</summary>
    public static readonly NodeTransform Transform = StructuralDirectiveFactory.Create(
        static name => name == "for",
        (element, directive, context) => ProcessFor(element, directive, context));

    private static Action? ProcessFor(ElementNode element, DirectiveNode directive, TransformContext context)
    {
        if (directive.Expression is not SimpleExpressionNode expression)
        {
            context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVForNoExpression, directive.Location));
            return null;
        }

        var parseResult = ForExpressionParser.Parse(expression);
        if (parseResult is null)
        {
            context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVForMalformedExpression, directive.Location));
            return null;
        }

        var workingFor = new WorkingFor
        {
            Source = parseResult.Source,
            ValueAlias = parseResult.Value,
            KeyAlias = parseResult.Key,
            ObjectIndexAlias = parseResult.Index,
            ParseResult = parseResult,
            Location = directive.Location,
        };

        var isTemplate = element.ElementType == ElementType.Template;
        if (isTemplate)
        {
            foreach (var child in element.Children)
            {
                workingFor.Children.Add(child);
            }
        }
        else
        {
            workingFor.Children.Add(element);
        }

        // v-memo on a v-for element is handled here (per-item memoization in the render-list loop). Mark the
        // inner element as seen so transformMemo does not also wrap it — mirroring upstream's WeakSet guard,
        // which the immutable model breaks because the structural factory produces a fresh reduced element.
        if (!isTemplate && TransformUtilities.FindDirective(element, "memo") is not null)
        {
            context.SeenMemo.Add(element);
        }

        context.ReplaceNode(workingFor);
        context.ScopeVFor++;

        var onExit = ProcessCodegen(element, directive, context, workingFor, parseResult, isTemplate);

        return () =>
        {
            context.ScopeVFor--;
            onExit?.Invoke();
        };
    }

    private static Action ProcessCodegen(
        ElementNode element,
        DirectiveNode directive,
        TransformContext context,
        WorkingFor workingFor,
        ForParseResult parseResult,
        bool isTemplate)
    {
        var renderExpArguments = new List<object> { workingFor.Source };
        var memo = TransformUtilities.FindDirective(element, "memo");
        var keyProperty = ResolveKeyProperty(element, out var keyExpression);

        var isStableFragment = workingFor.Source is SimpleExpressionNode source &&
                               source.ConstantType > ConstantType.NotConstant;
        var fragmentFlag = isStableFragment
            ? PatchFlags.StableFragment
            : keyProperty is not null ? PatchFlags.KeyedFragment : PatchFlags.UnkeyedFragment;

        var renderExpression = Ir.CallExpression(context.Helper(HelperNames.RenderList), renderExpArguments);
        workingFor.CodegenNode = context.CreateVNodeCall(
            context.Helper(HelperNames.Fragment),
            null,
            renderExpression,
            fragmentFlag,
            null,
            null,
            isBlock: true,
            disableTracking: !isStableFragment,
            isComponent: false,
            directive.Location);

        return () =>
        {
            var children = workingFor.Children;

            // <template v-for> key must be on the <template>, not a child.
            if (isTemplate)
            {
                foreach (var child in element.Children)
                {
                    if (child is ElementNode childElement)
                    {
                        var childKey = TransformUtilities.FindProperty(childElement, "key");
                        if (childKey is not null)
                        {
                            context.ReportError(CompilerErrorFactory.Create(
                                CompilerErrorCode.XVForTemplateKeyPlacement, childKey.Location));
                            break;
                        }
                    }
                }
            }

            var needFragmentWrapper = children.Count != 1 || children[0] is not ElementNode;
            var slotOutlet = ResolveSlotOutlet(element, isTemplate);

            TemplateSyntaxNode childBlock;
            if (slotOutlet is not null)
            {
                childBlock = context.GetCodegenNode(slotOutlet)!;
                if (isTemplate && keyProperty is not null)
                {
                    childBlock = TransformUtilities.InjectProperty(childBlock, keyProperty, context);
                }
            }
            else if (needFragmentWrapper)
            {
                childBlock = context.CreateVNodeCall(
                    context.Helper(HelperNames.Fragment),
                    keyProperty is not null ? Ir.ObjectExpression(new[] { keyProperty }) : null,
                    TransformFreeze.FreezeChildren(children),
                    PatchFlags.StableFragment,
                    null,
                    null,
                    isBlock: true,
                    disableTracking: false,
                    isComponent: false);
            }
            else
            {
                var elementBlock = (VNodeCall)context.GetCodegenNode(children[0])!;
                if (isTemplate && keyProperty is not null)
                {
                    elementBlock = (VNodeCall)TransformUtilities.InjectProperty(elementBlock, keyProperty, context);
                }

                var targetIsBlock = !isStableFragment;
                if (elementBlock.IsBlock != targetIsBlock)
                {
                    if (elementBlock.IsBlock)
                    {
                        context.RemoveHelper(HelperNames.OpenBlock);
                        context.RemoveHelper(TransformContext.GetVNodeBlockHelper(context.InSSR, elementBlock.IsComponent));
                    }
                    else
                    {
                        context.RemoveHelper(TransformContext.GetVNodeHelper(context.InSSR, elementBlock.IsComponent));
                    }
                }

                elementBlock = elementBlock with { IsBlock = targetIsBlock };
                if (targetIsBlock)
                {
                    context.Helper(HelperNames.OpenBlock);
                    context.Helper(TransformContext.GetVNodeBlockHelper(context.InSSR, elementBlock.IsComponent));
                }
                else
                {
                    context.Helper(TransformContext.GetVNodeHelper(context.InSSR, elementBlock.IsComponent));
                }

                context.SetCodegenNode(children[0], elementBlock);
                childBlock = elementBlock;
            }

            if (memo is not null)
            {
                var loopParameters = CreateForLoopParameters(parseResult, new object[] { Ir.SimpleExpression("_cached") });
                var body = BuildMemoLoopBody(memo, keyExpression, childBlock, context);
                var loop = Ir.FunctionExpression(loopParameters) with { Body = body };
                renderExpArguments.Add(loop);
                renderExpArguments.Add(Ir.SimpleExpression("_cache"));
                renderExpArguments.Add(Ir.SimpleExpression(context.CacheCount.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                context.AppendEmptyCacheSlot();
            }
            else
            {
                renderExpArguments.Add(Ir.FunctionExpression(CreateForLoopParameters(parseResult), childBlock, newline: true));
            }

            var finalRenderExpression = renderExpression with { Arguments = new SyntaxList<object>(renderExpArguments.ToArray()) };
            workingFor.CodegenNode = ((VNodeCall)workingFor.CodegenNode!) with { Children = finalRenderExpression };
        };
    }

    private static Property? ResolveKeyProperty(ElementNode element, out ExpressionNode? keyExpression)
    {
        keyExpression = null;
        var keyProperty = TransformUtilities.FindProperty(element, "key", dynamicOnly: false, allowEmpty: true);
        if (keyProperty is null)
        {
            return null;
        }

        if (keyProperty is AttributeNode attribute)
        {
            keyExpression = attribute.Value is not null ? Ir.SimpleExpression(attribute.Value.Content, true) : null;
        }
        else if (keyProperty is DirectiveNode directive)
        {
            keyExpression = directive.Expression ??
                            (directive.Argument is SimpleExpressionNode argument
                                ? VBindTransform.CreateShorthandExpression(argument)
                                : null);
        }

        return keyExpression is not null ? Ir.ObjectProperty("key", keyExpression) : null;
    }

    private static ElementNode? ResolveSlotOutlet(ElementNode element, bool isTemplate)
    {
        if (element.ElementType == ElementType.Slot)
        {
            return element;
        }

        if (isTemplate && element.Children.Count == 1 && element.Children[0] is ElementNode { ElementType: ElementType.Slot } slot)
        {
            return slot;
        }

        return null;
    }

    private static BlockStatement BuildMemoLoopBody(
        DirectiveNode memo,
        ExpressionNode? keyExpression,
        TemplateSyntaxNode childBlock,
        TransformContext context)
    {
        var conditionParts = new List<object> { "if (_cached" };
        if (keyExpression is not null)
        {
            conditionParts.Add(" && _cached.key === ");
            conditionParts.Add(keyExpression);
        }

        conditionParts.Add($" && {context.HelperString(HelperNames.IsMemoSame)}(_cached, _memo)) return _cached");

        return Ir.BlockStatement(new object[]
        {
            Ir.CompoundExpression("const _memo = (", memo.Expression!, ")"),
            Ir.CompoundExpression(conditionParts.ToArray()),
            Ir.CompoundExpression("const _item = ", childBlock),
            Ir.SimpleExpression("_item.memo = _memo"),
            Ir.SimpleExpression("return _item"),
        });
    }

    // Port of createForLoopParams / createParamsList.
    internal static IReadOnlyList<object> CreateForLoopParameters(ForParseResult parseResult, IReadOnlyList<object>? memoArguments = null)
    {
        var arguments = new List<ExpressionNode?> { parseResult.Value, parseResult.Key, parseResult.Index };
        if (memoArguments is not null)
        {
            foreach (var argument in memoArguments)
            {
                arguments.Add((ExpressionNode)argument);
            }
        }

        var last = -1;
        for (var index = arguments.Count - 1; index >= 0; index--)
        {
            if (arguments[index] is not null)
            {
                last = index;
                break;
            }
        }

        var result = new List<object>(last + 1);
        for (var index = 0; index <= last; index++)
        {
            result.Add(arguments[index] ?? (object)Ir.SimpleExpression(new string('_', index + 1), false));
        }

        return result;
    }
}
