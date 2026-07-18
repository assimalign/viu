namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The reduced constant-type analysis the transform pipeline needs — an opaque-expression subset of Vue
/// 3.5's <c>getConstantType</c> (<c>@vue/compiler-core</c> <c>transforms/cacheStatic.ts</c>).
/// </summary>
/// <remarks>
/// Because this stage keeps expression bodies opaque (upstream's non-<c>prefixIdentifiers</c> mode), the
/// analysis reads the <see cref="ConstantType"/> the parser stamped on each <see cref="SimpleExpressionNode"/>
/// rather than walking a JavaScript AST. The full element-subtree constant analysis that drives static
/// hoisting is deferred to [V01.01.05.07]; element nodes therefore report
/// <see cref="ConstantType.NotConstant"/> here. Only the expression/text cases matter for the patch-flag and
/// text-vnode decisions made in [V01.01.05.02]/[V01.01.05.03].
/// </remarks>
internal static class ConstantAnalysis
{
    /// <summary>Returns the static-ness level of <paramref name="node"/> (upstream <c>getConstantType</c>, reduced).</summary>
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
                // Element / container constant analysis (for static hoisting) is [V01.01.05.07]'s job.
                return ConstantType.NotConstant;
        }
    }
}
