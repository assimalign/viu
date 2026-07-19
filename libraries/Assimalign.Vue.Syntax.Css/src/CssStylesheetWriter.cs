using System;
using System.Text;

namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// Serializes a parsed (or rewritten) <see cref="CssStylesheetNode"/> back to CSS text with no scoping —
/// the plain counterpart of <see cref="CssScopedRewriter"/>. It is the serializer the composition-root
/// generator uses for a non-<c>scoped</c> <c>@style module</c> or <c>v-bind()</c> block, whose tree
/// <see cref="CssModuleRewriter"/> / <see cref="CssBindingRewriter"/> rewrote (renamed class selectors,
/// rewritten declaration values) but which carries no <c>data-v-</c> attribute. Selectors are rendered from
/// the parsed parts (so a renamed class's <c>Text</c> is what appears), not from the now-stale raw prelude.
/// </summary>
/// <remarks>
/// Output uses the same deterministic canonical form as <see cref="CssScopedRewriter"/> — two-space
/// indentation, <c>prop: value;</c>, <c>selector {</c> — so identical input yields identical output (the
/// incremental-caching contract) and a scoped and non-scoped block of the same component format alike.
/// Because the form is canonical, a non-scoped block that <em>is</em> rewritten no longer round-trips its
/// original whitespace or comments, exactly as a scoped block does not (see the Css <c>DESIGN.md</c>); a
/// non-scoped block with neither modules nor <c>v-bind()</c> is still emitted verbatim by the generator and
/// never reaches this writer.
/// </remarks>
public static class CssStylesheetWriter
{
    /// <summary>Serializes <paramref name="stylesheet"/> to canonical, unscoped CSS text.</summary>
    /// <param name="stylesheet">The stylesheet to serialize.</param>
    /// <returns>The deterministic CSS text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stylesheet"/> is <see langword="null"/>.</exception>
    public static string Write(CssStylesheetNode stylesheet)
    {
        if (stylesheet is null)
        {
            throw new ArgumentNullException(nameof(stylesheet));
        }

        var builder = new StringBuilder();
        WriteRules(stylesheet.Rules, indent: 0, builder);
        return builder.ToString();
    }

    private static void WriteRules(SyntaxList<CssSyntaxNode> rules, int indent, StringBuilder builder)
    {
        foreach (var rule in rules)
        {
            switch (rule)
            {
                case CssQualifiedRuleNode qualified:
                    WriteQualifiedRule(qualified, indent, builder);
                    break;
                case CssAtRuleNode atRule:
                    WriteAtRule(atRule, indent, builder);
                    break;
                case CssKeyframeRuleNode keyframe:
                    WriteKeyframeRule(keyframe, indent, builder);
                    break;
                case CssDeclarationNode declaration:
                    WriteDeclaration(declaration, indent, builder);
                    break;
            }
        }
    }

    private static void WriteQualifiedRule(CssQualifiedRuleNode rule, int indent, StringBuilder builder)
    {
        AppendIndent(builder, indent);
        builder.Append(RenderSelectorList(rule.Selectors)).Append(" {\n");
        foreach (var declaration in rule.Declarations)
        {
            WriteDeclaration(declaration, indent + 1, builder);
        }

        AppendIndent(builder, indent);
        builder.Append("}\n");
    }

    private static void WriteAtRule(CssAtRuleNode atRule, int indent, StringBuilder builder)
    {
        AppendIndent(builder, indent);
        builder.Append('@').Append(atRule.Name);
        if (atRule.Prelude.Length > 0)
        {
            builder.Append(' ').Append(atRule.Prelude);
        }

        if (!atRule.HasBlock)
        {
            builder.Append(";\n");
            return;
        }

        builder.Append(" {\n");
        WriteRules(atRule.Body, indent + 1, builder);
        AppendIndent(builder, indent);
        builder.Append("}\n");
    }

    private static void WriteKeyframeRule(CssKeyframeRuleNode rule, int indent, StringBuilder builder)
    {
        AppendIndent(builder, indent);
        builder.Append(rule.Selector).Append(" {\n");
        foreach (var declaration in rule.Declarations)
        {
            WriteDeclaration(declaration, indent + 1, builder);
        }

        AppendIndent(builder, indent);
        builder.Append("}\n");
    }

    private static void WriteDeclaration(CssDeclarationNode declaration, int indent, StringBuilder builder)
    {
        AppendIndent(builder, indent);
        builder.Append(declaration.Property).Append(": ").Append(declaration.Value);
        if (declaration.Important)
        {
            builder.Append(" !important");
        }

        builder.Append(";\n");
    }

    private static string RenderSelectorList(CssSelectorListNode list)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < list.Selectors.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            RenderComplexSelector(list.Selectors[index], builder);
        }

        return builder.ToString();
    }

    private static void RenderComplexSelector(CssComplexSelectorNode complex, StringBuilder builder)
    {
        foreach (var part in complex.Parts)
        {
            switch (part)
            {
                case CssCombinatorNode combinator:
                    builder.Append(RenderCombinator(combinator.Combinator));
                    break;
                case CssSimpleSelectorNode simple:
                    builder.Append(simple.Text);
                    break;
                case CssPseudoSelectorNode pseudo:
                    // Pseudos are never rewritten by the module/binding passes, so the exact authored slice
                    // is still faithful.
                    builder.Append(pseudo.Location.Source);
                    break;
            }
        }
    }

    private static string RenderCombinator(CssCombinatorKind kind)
        => kind switch
        {
            CssCombinatorKind.Child => " > ",
            CssCombinatorKind.NextSibling => " + ",
            CssCombinatorKind.SubsequentSibling => " ~ ",
            _ => " ",
        };

    private static void AppendIndent(StringBuilder builder, int levels)
    {
        for (var level = 0; level < levels; level++)
        {
            builder.Append("  ");
        }
    }
}
