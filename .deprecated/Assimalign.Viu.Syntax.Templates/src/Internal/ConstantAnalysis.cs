namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The constant-type analysis that drives static caching and stringification — the C# port of Vue 3.5's
/// <c>getConstantType</c> family (<c>@vue/compiler-core</c>
/// <see href="https://github.com/vuejs/core/blob/v3.5.13/packages/compiler-core/src/transforms/cacheStatic.ts">transforms/cacheStatic.ts</see>).
/// </summary>
/// <remarks>
/// <para>
/// Two overloads exist. The context-free <see cref="GetConstantType(TemplateSyntaxNode)"/> reads the
/// <see cref="ConstantType"/> the parser stamped on each <see cref="SimpleExpressionNode"/> and is what the
/// patch-flag inference in <see cref="TransformElement"/> uses; it treats element/container nodes as
/// <see cref="ConstantType.NotConstant"/> because it cannot see their code-generation nodes. The
/// context-aware <see cref="GetConstantType(TemplateSyntaxNode, TransformContext)"/> is the full subtree
/// analysis the static-caching walk ([V01.01.05.07]) uses: it resolves an element's
/// <see cref="VNodeCall"/> through the transform side table, propagates <see cref="ConstantType.NotConstant"/>
/// from any non-constant child, prop, or <c>v-bind</c> expression, memoizes each element's level in
/// <see cref="TransformContext.ConstantCache"/>, and — matching upstream — demotes a fully static
/// <c>svg</c>/<c>foreignObject</c>/<c>math</c> block back to a plain vnode as a side effect.
/// </para>
/// <para>
/// Because Viu keeps expression bodies opaque (upstream's non-<c>prefixIdentifiers</c> mode), an
/// interpolation or a dynamic <c>v-bind</c> value is never classified above
/// <see cref="ConstantType.NotConstant"/>; only static text, static attributes, and compiler-injected
/// literal constants (<c>v-if</c> branch keys, <c>v-model</c> modifier objects) reach the higher levels.
/// </para>
/// </remarks>
internal static class ConstantAnalysis
{
    /// <summary>
    /// Returns the static-ness level of <paramref name="node"/> without a transform context (upstream
    /// <c>getConstantType</c> reduced to the cases the patch-flag pass needs). Element and container nodes
    /// report <see cref="ConstantType.NotConstant"/>; use the context-aware overload for subtree analysis.
    /// </summary>
    /// <param name="node">The node to analyze.</param>
    public static ConstantType GetConstantType(TemplateSyntaxNode node)
    {
        switch (node)
        {
            case SimpleExpressionNode simple:
                return simple.ConstantType;
            case TextNode:
                return ConstantType.CanStringify;
            case InterpolationNode interpolation:
                return GetConstantType(interpolation.Content);
            case TextCallNode textCall:
                return GetConstantType(textCall.Content);
            case CompoundExpressionNode compound:
                var result = ConstantType.CanStringify;
                foreach (var part in compound.Parts)
                {
                    if (part is string)
                    {
                        continue;
                    }

                    if (part is TemplateSyntaxNode child)
                    {
                        var childType = GetConstantType(child);
                        if (childType == ConstantType.NotConstant)
                        {
                            return ConstantType.NotConstant;
                        }

                        if (childType < result)
                        {
                            result = childType;
                        }
                    }
                }

                return result;
            default:
                // Element / container constant analysis needs the code-generation side table — see the
                // context-aware overload below.
                return ConstantType.NotConstant;
        }
    }

    /// <summary>
    /// Returns the static-ness level of <paramref name="node"/>, resolving elements' code-generation nodes
    /// through <paramref name="context"/> — the full port of upstream <c>getConstantType</c>. Memoized in
    /// <see cref="TransformContext.ConstantCache"/>.
    /// </summary>
    /// <param name="node">The node to analyze.</param>
    /// <param name="context">The transform context (side table, constant cache, helper registration).</param>
    public static ConstantType GetConstantType(TemplateSyntaxNode node, TransformContext context)
    {
        switch (node)
        {
            case ElementNode element:
                return GetElementConstantType(element, context);
            case CommentNode:
                return ConstantType.CanStringify;
            case IfNode or WorkingIf or ForNode or WorkingFor or IfBranchNode or WorkingIfBranch:
                return ConstantType.NotConstant;
            case CacheExpression:
                return ConstantType.CanCache;
            default:
                // Simple expression, text, interpolation, text call, compound: these never contain an
                // element, so the context-free overload is exact.
                return GetConstantType(node);
        }
    }

    private static ConstantType GetElementConstantType(ElementNode element, TransformContext context)
    {
        if (element.ElementType != ElementType.Element)
        {
            return ConstantType.NotConstant;
        }

        if (context.ConstantCache.TryGetValue(element, out var cached))
        {
            return cached;
        }

        if (context.GetCodegenNode(element) is not VNodeCall codegenNode)
        {
            return ConstantType.NotConstant;
        }

        // A block (a v-for fragment, a component-conditioned block, etc.) is never constant, except the
        // svg/foreignObject/math elements the DOM transform makes blocks for namespace tracking — those are
        // demoted below when they turn out static.
        if (codegenNode.IsBlock &&
            element.Tag != "svg" && element.Tag != "foreignObject" && element.Tag != "math")
        {
            return ConstantType.NotConstant;
        }

        if (codegenNode.PatchFlag is not null)
        {
            // The element has a patch flag, so it is not constant.
            context.ConstantCache[element] = ConstantType.NotConstant;
            return ConstantType.NotConstant;
        }

        var returnType = ConstantType.CanStringify;

        // 1. Even with no patch flag the generated props can carry non-hoistable expressions (compiler
        // injected keys, cached handlers), so always check them.
        var generatedPropsType = GetGeneratedPropsConstantType(element, context);
        if (generatedPropsType == ConstantType.NotConstant)
        {
            context.ConstantCache[element] = ConstantType.NotConstant;
            return ConstantType.NotConstant;
        }

        if (generatedPropsType < returnType)
        {
            returnType = generatedPropsType;
        }

        // 2. Its children.
        foreach (var child in element.Children)
        {
            var childType = GetConstantType(child, context);
            if (childType == ConstantType.NotConstant)
            {
                context.ConstantCache[element] = ConstantType.NotConstant;
                return ConstantType.NotConstant;
            }

            if (childType < returnType)
            {
                returnType = childType;
            }
        }

        // 3. Above CAN_SKIP_PATCH, check whether any v-bind expression lowers the type.
        if (returnType > ConstantType.CanSkipPatch)
        {
            foreach (var property in element.Properties)
            {
                if (property is DirectiveNode { Name: "bind" } directive && directive.Expression is not null)
                {
                    var expressionType = GetConstantType(directive.Expression, context);
                    if (expressionType == ConstantType.NotConstant)
                    {
                        context.ConstantCache[element] = ConstantType.NotConstant;
                        return ConstantType.NotConstant;
                    }

                    if (expressionType < returnType)
                    {
                        returnType = expressionType;
                    }
                }
            }
        }

        // Only svg/foreignObject/math reach here as blocks; a static one needs no block because there are no
        // nested updates, so demote it — but bail if it carries a custom directive.
        if (codegenNode.IsBlock)
        {
            foreach (var property in element.Properties)
            {
                if (property is DirectiveNode)
                {
                    context.ConstantCache[element] = ConstantType.NotConstant;
                    return ConstantType.NotConstant;
                }
            }

            context.RemoveHelper(HelperNames.OpenBlock);
            context.RemoveHelper(TransformContext.GetVNodeBlockHelper(context.InSSR, codegenNode.IsComponent));
            context.SetCodegenNode(element, codegenNode with { IsBlock = false });
            context.Helper(TransformContext.GetVNodeHelper(context.InSSR, codegenNode.IsComponent));
        }

        context.ConstantCache[element] = returnType;
        return returnType;
    }

    /// <summary>
    /// The constant-type of an element's generated props object (upstream <c>getGeneratedPropsConstantType</c>):
    /// every key and value must be constant, and helper-wrapped values (<c>normalizeClass</c>, …) are
    /// unwrapped to their argument's constant-type.
    /// </summary>
    /// <param name="element">The element whose <see cref="VNodeCall.Props"/> is analyzed.</param>
    /// <param name="context">The transform context.</param>
    public static ConstantType GetGeneratedPropsConstantType(ElementNode element, TransformContext context)
    {
        var returnType = ConstantType.CanStringify;
        if (context.GetCodegenNode(element) is not VNodeCall { Props: ObjectExpression properties })
        {
            return returnType;
        }

        foreach (var property in properties.Properties)
        {
            var keyType = GetConstantType(property.Key, context);
            if (keyType == ConstantType.NotConstant)
            {
                return ConstantType.NotConstant;
            }

            if (keyType < returnType)
            {
                returnType = keyType;
            }

            ConstantType valueType;
            if (property.Value is SimpleExpressionNode)
            {
                valueType = GetConstantType(property.Value, context);
            }
            else if (property.Value is CallExpression call)
            {
                // Some helper calls can be hoisted (e.g. the compiler-generated normalizeProps for a
                // pre-normalized class); respect the constant-type of the helper's argument.
                valueType = GetConstantTypeOfHelperCall(call, context);
            }
            else
            {
                valueType = ConstantType.NotConstant;
            }

            if (valueType == ConstantType.NotConstant)
            {
                return ConstantType.NotConstant;
            }

            if (valueType < returnType)
            {
                returnType = valueType;
            }
        }

        return returnType;
    }

    private static ConstantType GetConstantTypeOfHelperCall(CallExpression call, TransformContext context)
    {
        if (call.Callee is RuntimeHelper helper && IsHoistableHelper(helper) && call.Arguments.Count > 0)
        {
            var argument = call.Arguments[0];
            if (argument is SimpleExpressionNode simple)
            {
                return GetConstantType(simple, context);
            }

            if (argument is CallExpression nested)
            {
                // Nested helper call, e.g. normalizeProps(guardReactiveProps(exp)).
                return GetConstantTypeOfHelperCall(nested, context);
            }
        }

        return ConstantType.NotConstant;
    }

    // Upstream allowHoistedHelperSet: the pure prop-normalization helpers whose result is constant when
    // their argument is (@vue/compiler-core cacheStatic.ts).
    private static bool IsHoistableHelper(RuntimeHelper helper)
        => helper == HelperNames.NormalizeClass ||
           helper == HelperNames.NormalizeStyle ||
           helper == HelperNames.NormalizeProps ||
           helper == HelperNames.GuardReactiveProps;
}
