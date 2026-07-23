using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// Rewrites a parsed <see cref="CssStylesheetNode"/> for CSS Modules — the pure-.NET C# port of Vue 3.5's
/// <c>@style module</c> compilation (<c>@vue/compiler-sfc</c> <c>compileStyle()</c> with
/// <c>postcss-modules</c>, https://vuejs.org/api/sfc-css-features.html#css-modules). Every local class
/// selector <c>.foo</c> is renamed to a deterministic, component-scoped hashed name and the original →
/// hashed map is returned so the composition-root generator can emit the typed <c>$style</c>-equivalent
/// accessor ([V01.01.06.06], issue #62).
/// </summary>
/// <remarks>
/// <para>
/// <b>The hashing scheme.</b> A class <c>foo</c> becomes <c>foo_&lt;hash&gt;</c>, where <c>&lt;hash&gt;</c>
/// is the eight-hex-digit FNV-1a of <c>&lt;localHashSalt&gt;-foo</c> (<see cref="CssHash"/>). The caller
/// passes the component's short scope id (the <c>data-v-</c> hash) as the salt, so the same class in two
/// components hashes differently (per-component uniqueness) while the same class in the same component is
/// stable across rebuilds (the asset-caching contract) — consistent with the [V01.01.06.04] scope-id
/// scheme. Keeping the original name as a readable prefix mirrors <c>postcss-modules</c>' default
/// <c>[local]_[hash]</c> shape and aids debugging the emitted CSS.
/// </para>
/// <para>
/// <b>Scope of the rename.</b> Only class selectors in normal compound position are renamed. Class names
/// nested inside functional pseudo arguments — <c>:not(.foo)</c>, <c>:deep(.foo)</c>, <c>:slotted(.foo)</c>,
/// <c>:global(.foo)</c> — are <em>not</em> renamed: <c>:deep</c>/<c>:global</c> deliberately target
/// external / un-hashed names, and the parser keeps non-reserved functional-pseudo arguments as verbatim
/// text (see the Css <c>DESIGN.md</c> non-goals). Composing with <c>scoped</c> is order-independent because
/// the rename only rewrites the parsed <c>Text</c> the scoped serializer reads.
/// </para>
/// </remarks>
public static class CssModuleRewriter
{
    /// <summary>
    /// Rewrites <paramref name="stylesheet"/>'s local class selectors to hashed names salted by
    /// <paramref name="localHashSalt"/>.
    /// </summary>
    /// <param name="stylesheet">The parsed stylesheet to rewrite.</param>
    /// <param name="localHashSalt">The component-scoped salt (the short <c>data-v-</c> scope id).</param>
    /// <returns>The rewritten stylesheet and the original → hashed class map.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stylesheet"/> or <paramref name="localHashSalt"/> is <see langword="null"/>.</exception>
    public static CssModuleRewriteResult Rewrite(CssStylesheetNode stylesheet, string localHashSalt)
    {
        if (stylesheet is null)
        {
            throw new ArgumentNullException(nameof(stylesheet));
        }

        if (localHashSalt is null)
        {
            throw new ArgumentNullException(nameof(localHashSalt));
        }

        var classes = new Dictionary<string, string>(StringComparer.Ordinal);
        var rewritten = stylesheet with { Rules = RewriteRules(stylesheet.Rules, localHashSalt, classes) };
        return new CssModuleRewriteResult(rewritten, classes);
    }

    private static SyntaxList<CssSyntaxNode> RewriteRules(
        SyntaxList<CssSyntaxNode> rules,
        string salt,
        Dictionary<string, string> classes)
    {
        var rewritten = new CssSyntaxNode[rules.Count];
        for (var index = 0; index < rules.Count; index++)
        {
            rewritten[index] = rules[index] switch
            {
                CssQualifiedRuleNode qualified => qualified with
                {
                    Selectors = RewriteSelectorList(qualified.Selectors, salt, classes),
                },
                // Conditional-group at-rules (@media/@supports/...) nest qualified rules; recurse so class
                // selectors inside them are renamed too. @keyframes/@font-face bodies carry no class
                // selectors, so recursion is a structural copy there.
                CssAtRuleNode atRule => atRule with { Body = RewriteRules(atRule.Body, salt, classes) },
                var other => other,
            };
        }

        return new SyntaxList<CssSyntaxNode>(rewritten);
    }

    private static CssSelectorListNode RewriteSelectorList(
        CssSelectorListNode list,
        string salt,
        Dictionary<string, string> classes)
    {
        var complexSelectors = new CssComplexSelectorNode[list.Selectors.Count];
        for (var index = 0; index < list.Selectors.Count; index++)
        {
            complexSelectors[index] = RewriteComplexSelector(list.Selectors[index], salt, classes);
        }

        return list with { Selectors = new SyntaxList<CssComplexSelectorNode>(complexSelectors) };
    }

    private static CssComplexSelectorNode RewriteComplexSelector(
        CssComplexSelectorNode complex,
        string salt,
        Dictionary<string, string> classes)
    {
        var parts = new CssSelectorPartNode[complex.Parts.Count];
        for (var index = 0; index < complex.Parts.Count; index++)
        {
            var part = complex.Parts[index];
            parts[index] = part is CssSimpleSelectorNode { Selector: CssSimpleSelectorKind.Class } classSelector
                ? classSelector with { Text = RenameClass(classSelector.Text, salt, classes) }
                : part;
        }

        return complex with { Parts = new SyntaxList<CssSelectorPartNode>(parts) };
    }

    // ".foo" -> ".foo_<hash>", recording foo -> foo_<hash> on first sight (a class used twice keeps its
    // first hash, matching the one-name-one-hash module contract).
    private static string RenameClass(string classText, string salt, Dictionary<string, string> classes)
    {
        // A class simple selector's Text is always ".<ident>" (the parser only classifies a '.' + ident run
        // as Class), but guard defensively so a malformed slice is passed through untouched.
        if (classText.Length < 2 || classText[0] != '.')
        {
            return classText;
        }

        var original = classText.Substring(1);
        if (!classes.TryGetValue(original, out var hashed))
        {
            hashed = original + "_" + CssHash.Compute(salt + "-" + original);
            classes[original] = hashed;
        }

        return "." + hashed;
    }
}
