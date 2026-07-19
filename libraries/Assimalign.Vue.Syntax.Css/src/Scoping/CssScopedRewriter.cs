using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// Rewrites a parsed <see cref="CssStylesheetNode"/> into attribute-scoped CSS text, the pure-.NET C#
/// port of Vue 3.5's scoped-CSS transform (<c>@vue/compiler-sfc</c> <c>compileStyle()</c> with the
/// scoped PostCSS plugin, <c>pluginScoped.ts</c>; https://vuejs.org/api/sfc-css-features.html). Each
/// complex selector gains a <c>[data-v-hash]</c> attribute on the last compound, and Vue's reserved
/// pseudo-combinators are honored:
/// <list type="bullet">
/// <item><c>:deep(inner)</c> / <c>::v-deep(inner)</c> — the attribute lands on the compound before the deep, then a descendant combinator, and <c>inner</c> is left unscoped.</item>
/// <item><c>:slotted(inner)</c> / <c>::v-slotted(inner)</c> — <c>inner</c> is scoped with the slotted suffix <c>[data-v-hash-s]</c>.</item>
/// <item><c>:global(inner)</c> / <c>::v-global(inner)</c> — the whole selector becomes <c>inner</c>, unscoped.</item>
/// </list>
/// <c>@keyframes</c> names are suffixed with the scope's short id and referencing <c>animation</c> /
/// <c>animation-name</c> declaration values are rewritten to match. Serialization is deterministic
/// (canonical two-space indentation), so identical input yields identical output — the incremental
/// caching contract. Legacy <c>&gt;&gt;&gt;</c>/<c>/deep/</c> combinators and the deep-inside-
/// <c>:is()</c>/<c>:where()</c>/<c>:not()</c> split are deliberate non-goals (see DESIGN.md).
/// </summary>
public static class CssScopedRewriter
{
    private const string ScopeIdPrefix = "data-v-";

    /// <summary>
    /// Rewrites <paramref name="stylesheet"/> into scoped CSS text using <paramref name="scopeId"/> (the
    /// full attribute id, e.g. <c>data-v-abc12345</c>; the keyframes suffix is its short form with the
    /// <c>data-v-</c> prefix stripped).
    /// </summary>
    /// <param name="stylesheet">The parsed stylesheet to rewrite.</param>
    /// <param name="scopeId">The scope id — the attribute name stamped on scoped elements.</param>
    /// <returns>The deterministic scoped CSS text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stylesheet"/> or <paramref name="scopeId"/> is <see langword="null"/>.</exception>
    public static string Rewrite(CssStylesheetNode stylesheet, string scopeId)
    {
        if (stylesheet is null)
        {
            throw new ArgumentNullException(nameof(stylesheet));
        }

        if (scopeId is null)
        {
            throw new ArgumentNullException(nameof(scopeId));
        }

        var shortId = scopeId.StartsWith(ScopeIdPrefix, StringComparison.Ordinal)
            ? scopeId.Substring(ScopeIdPrefix.Length)
            : scopeId;

        var keyframes = new Dictionary<string, string>(StringComparer.Ordinal);
        CollectKeyframes(stylesheet.Rules, shortId, keyframes);

        var context = new RewriteContext(scopeId, keyframes);
        var builder = new StringBuilder();
        SerializeRules(stylesheet.Rules, context, indent: 0, builder);
        return builder.ToString();
    }

    private static void CollectKeyframes(SyntaxList<CssSyntaxNode> rules, string shortId, Dictionary<string, string> keyframes)
    {
        foreach (var rule in rules)
        {
            if (rule is not CssAtRuleNode atRule)
            {
                continue;
            }

            if (IsKeyframes(atRule.Name))
            {
                var name = atRule.Prelude;
                var suffix = "-" + shortId;
                if (name.Length > 0 && !name.EndsWith(suffix, StringComparison.Ordinal) && !keyframes.ContainsKey(name))
                {
                    keyframes[name] = name + suffix;
                }
            }
            else if (IsConditionalGroup(atRule.Name))
            {
                // @keyframes can nest inside a conditional group (e.g. @media); collect those too.
                CollectKeyframes(atRule.Body, shortId, keyframes);
            }
        }
    }

    private static void SerializeRules(SyntaxList<CssSyntaxNode> rules, RewriteContext context, int indent, StringBuilder builder)
    {
        foreach (var rule in rules)
        {
            switch (rule)
            {
                case CssQualifiedRuleNode qualified:
                    SerializeQualifiedRule(qualified, context, indent, builder);
                    break;
                case CssAtRuleNode atRule:
                    SerializeAtRule(atRule, context, indent, builder);
                    break;
                case CssKeyframeRuleNode keyframe:
                    SerializeKeyframeRule(keyframe, context, indent, builder);
                    break;
                case CssDeclarationNode declaration:
                    SerializeDeclaration(declaration, context, indent, builder);
                    break;
            }
        }
    }

    private static void SerializeQualifiedRule(CssQualifiedRuleNode rule, RewriteContext context, int indent, StringBuilder builder)
    {
        var selector = RewriteSelectorList(rule.Selectors, context.ScopeId, slotted: false);
        AppendIndent(builder, indent);
        builder.Append(selector).Append(" {\n");
        foreach (var declaration in rule.Declarations)
        {
            SerializeDeclaration(declaration, context, indent + 1, builder);
        }

        AppendIndent(builder, indent);
        builder.Append("}\n");
    }

    private static void SerializeAtRule(CssAtRuleNode atRule, RewriteContext context, int indent, StringBuilder builder)
    {
        AppendIndent(builder, indent);
        builder.Append('@').Append(atRule.Name);

        if (IsKeyframes(atRule.Name))
        {
            var mappedName = context.Keyframes.TryGetValue(atRule.Prelude, out var renamed) ? renamed : atRule.Prelude;
            builder.Append(' ').Append(mappedName).Append(" {\n");
            foreach (var child in atRule.Body)
            {
                if (child is CssKeyframeRuleNode keyframe)
                {
                    SerializeKeyframeRule(keyframe, context, indent + 1, builder);
                }
            }

            AppendIndent(builder, indent);
            builder.Append("}\n");
            return;
        }

        if (!atRule.HasBlock)
        {
            if (atRule.Prelude.Length > 0)
            {
                builder.Append(' ').Append(atRule.Prelude);
            }

            builder.Append(";\n");
            return;
        }

        if (atRule.Prelude.Length > 0)
        {
            builder.Append(' ').Append(atRule.Prelude);
        }

        builder.Append(" {\n");
        SerializeRules(atRule.Body, context, indent + 1, builder);
        AppendIndent(builder, indent);
        builder.Append("}\n");
    }

    private static void SerializeKeyframeRule(CssKeyframeRuleNode rule, RewriteContext context, int indent, StringBuilder builder)
    {
        AppendIndent(builder, indent);
        builder.Append(rule.Selector).Append(" {\n");
        foreach (var declaration in rule.Declarations)
        {
            SerializeDeclaration(declaration, context, indent + 1, builder);
        }

        AppendIndent(builder, indent);
        builder.Append("}\n");
    }

    private static void SerializeDeclaration(CssDeclarationNode declaration, RewriteContext context, int indent, StringBuilder builder)
    {
        AppendIndent(builder, indent);
        builder.Append(declaration.Property).Append(": ").Append(RewriteDeclarationValue(declaration, context.Keyframes));
        if (declaration.Important)
        {
            builder.Append(" !important");
        }

        builder.Append(";\n");
    }

    // Rewrites animation / animation-name values so they reference the suffixed keyframe names, mirroring
    // pluginScoped.ts's OnceExit declaration handler.
    private static string RewriteDeclarationValue(CssDeclarationNode declaration, Dictionary<string, string> keyframes)
    {
        if (keyframes.Count == 0)
        {
            return declaration.Value;
        }

        var property = DeVendorPrefix(declaration.Property).ToLowerInvariant();
        if (string.Equals(property, "animation-name", StringComparison.Ordinal))
        {
            var names = declaration.Value.Split(',');
            for (var index = 0; index < names.Length; index++)
            {
                var trimmed = names[index].Trim();
                names[index] = keyframes.TryGetValue(trimmed, out var mapped) ? mapped : trimmed;
            }

            return string.Join(", ", names);
        }

        if (string.Equals(property, "animation", StringComparison.Ordinal))
        {
            var animations = declaration.Value.Split(',');
            for (var index = 0; index < animations.Length; index++)
            {
                animations[index] = RewriteAnimationShorthand(animations[index], keyframes);
            }

            return string.Join(", ", animations);
        }

        return declaration.Value;
    }

    // Splits one animation shorthand by whitespace and replaces the first component that names a scoped
    // keyframe (upstream finds the keyframe name among the space-separated parts and swaps it in place).
    private static string RewriteAnimationShorthand(string animation, Dictionary<string, string> keyframes)
    {
        var parts = animation.Trim().Split(new[] { ' ', '\t', '\n', '\r', '\f' }, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            if (keyframes.TryGetValue(parts[index], out var mapped))
            {
                parts[index] = mapped;
                break;
            }
        }

        return string.Join(" ", parts);
    }

    private static string RewriteSelectorList(CssSelectorListNode list, string scopeId, bool slotted)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < list.Selectors.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(RewriteComplexSelector(list.Selectors[index], scopeId, slotted));
        }

        return builder.ToString();
    }

    // The heart of the transform: mirrors pluginScoped.ts's rewriteSelector over the flat part list,
    // choosing where the [data-v-hash] attribute is inserted and honoring the reserved functional pseudos.
    private static string RewriteComplexSelector(CssComplexSelectorNode complex, string scopeId, bool slotted)
    {
        var rendered = new List<RenderedPart>();
        var shouldInject = true;
        var nodeIndex = -1;
        var sawCompoundOrPseudo = false;

        var parts = complex.Parts;
        var index = 0;
        while (index < parts.Count)
        {
            var part = parts[index];
            switch (part)
            {
                case CssCombinatorNode combinator:
                    rendered.Add(new RenderedPart(RenderCombinator(combinator.Combinator), true));
                    index++;
                    continue;

                case CssPseudoSelectorNode { Pseudo: CssPseudoSelectorKind.Global, Argument: { } globalInner }:
                    // The whole selector is replaced by the unscoped inner selector.
                    return globalInner.Location.Source;

                case CssPseudoSelectorNode { Pseudo: CssPseudoSelectorKind.Deep, Argument: { } deepInner }:
                    if (rendered.Count == 0 || !rendered[rendered.Count - 1].IsCombinator)
                    {
                        rendered.Add(new RenderedPart(" ", true));
                    }

                    // The deep inner selector escapes scoping and is emitted verbatim; nothing after it is scoped.
                    rendered.Add(new RenderedPart(deepInner.Location.Source, false));
                    index = parts.Count;
                    continue;

                case CssPseudoSelectorNode { Pseudo: CssPseudoSelectorKind.Slotted, Argument: { } slottedInner }:
                    rendered.Add(new RenderedPart(RewriteSelectorList(slottedInner, scopeId, slotted: true), false));
                    shouldInject = false;
                    index = parts.Count;
                    continue;

                case CssPseudoSelectorNode pseudo:
                    // A normal pseudo (or a reserved pseudo used without an argument) is emitted verbatim
                    // and never becomes the attribute anchor.
                    rendered.Add(new RenderedPart(pseudo.Location.Source, false));
                    sawCompoundOrPseudo = true;
                    index++;
                    continue;

                case CssSimpleSelectorNode { Selector: CssSimpleSelectorKind.Universal }:
                    if (!sawCompoundOrPseudo && nodeIndex == -1)
                    {
                        // A leading universal is dropped; a following descendant combinator is dropped with it.
                        if (index + 1 < parts.Count && parts[index + 1] is CssCombinatorNode { Combinator: CssCombinatorKind.Descendant })
                        {
                            index += 2;
                        }
                        else
                        {
                            index += 1;
                        }

                        continue;
                    }

                    // A non-leading universal is kept but never becomes the anchor (the attribute stays on
                    // the preceding compound).
                    rendered.Add(new RenderedPart("*", false));
                    index++;
                    continue;

                case CssSimpleSelectorNode simple:
                    rendered.Add(new RenderedPart(simple.Text, false));
                    nodeIndex = rendered.Count - 1;
                    sawCompoundOrPseudo = true;
                    index++;
                    continue;

                default:
                    index++;
                    continue;
            }
        }

        var attribute = "[" + (slotted ? scopeId + "-s" : scopeId) + "]";
        var builder = new StringBuilder();
        if (shouldInject && nodeIndex == -1)
        {
            builder.Append(attribute);
        }

        for (var j = 0; j < rendered.Count; j++)
        {
            builder.Append(rendered[j].Text);
            if (shouldInject && j == nodeIndex)
            {
                builder.Append(attribute);
            }
        }

        return builder.ToString();
    }

    private static string RenderCombinator(CssCombinatorKind kind)
        => kind switch
        {
            CssCombinatorKind.Child => " > ",
            CssCombinatorKind.NextSibling => " + ",
            CssCombinatorKind.SubsequentSibling => " ~ ",
            _ => " ",
        };

    private static bool IsKeyframes(string name)
        => string.Equals(DeVendorPrefix(name), "keyframes", StringComparison.OrdinalIgnoreCase);

    private static bool IsConditionalGroup(string name)
    {
        var bare = DeVendorPrefix(name);
        return string.Equals(bare, "media", StringComparison.OrdinalIgnoreCase)
            || string.Equals(bare, "supports", StringComparison.OrdinalIgnoreCase)
            || string.Equals(bare, "container", StringComparison.OrdinalIgnoreCase)
            || string.Equals(bare, "layer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(bare, "document", StringComparison.OrdinalIgnoreCase)
            || string.Equals(bare, "scope", StringComparison.OrdinalIgnoreCase);
    }

    private static string DeVendorPrefix(string name)
    {
        if (name.Length > 1 && name[0] == '-')
        {
            var secondDash = name.IndexOf('-', 1);
            if (secondDash > 0 && secondDash < name.Length - 1)
            {
                return name.Substring(secondDash + 1);
            }
        }

        return name;
    }

    private static void AppendIndent(StringBuilder builder, int levels)
    {
        for (var level = 0; level < levels; level++)
        {
            builder.Append("  ");
        }
    }

    private readonly record struct RenderedPart(string Text, bool IsCombinator);

    private readonly record struct RewriteContext(string ScopeId, Dictionary<string, string> Keyframes);
}
