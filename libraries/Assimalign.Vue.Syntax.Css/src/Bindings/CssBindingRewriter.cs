using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// Rewrites <c>v-bind()</c> occurrences in a parsed <see cref="CssStylesheetNode"/>'s declaration values —
/// the pure-.NET C# port of Vue 3.5's <c>v-bind()</c>-in-CSS compilation (<c>@vue/compiler-sfc</c>
/// <c>cssVars.ts</c>, https://vuejs.org/api/sfc-css-features.html#v-bind-in-css). Each
/// <c>v-bind(expression)</c> is replaced with a component-scoped custom-property reference
/// <c>var(--&lt;hash&gt;)</c>, and the referenced expressions are collected so the composition-root
/// generator can record them as component metadata for the <c>UseCssVars</c> runtime ([V01.01.06.06],
/// issue #62).
/// </summary>
/// <remarks>
/// <para>
/// <b>The variable scheme.</b> A usage <c>v-bind(expr)</c> becomes <c>var(--&lt;hash&gt;)</c>, where
/// <c>&lt;hash&gt;</c> is the eight-hex-digit FNV-1a of <c>&lt;localHashSalt&gt;-&lt;expr&gt;</c>
/// (<see cref="CssHash"/>). The caller passes the component's short scope id as the salt — Vue's
/// <c>genVarName</c> likewise folds the file id into the hash — so the property is deterministic and
/// component-scoped, and the CSS <c>var(--&lt;hash&gt;)</c> matches the runtime's
/// <c>style.setProperty("--&lt;hash&gt;", …)</c> by construction because both derive from the same hash of
/// the same expression. Distinct expressions are de-duplicated by their trimmed, unquoted text (upstream's
/// <c>if (!vars.includes(variable))</c>), so a repeated <c>v-bind(color)</c> yields one binding.
/// </para>
/// <para>
/// <b>Extraction.</b> The scan mirrors upstream's <c>lexBinding</c>: it walks each declaration value,
/// skipping string literals and <c>/* … */</c> comments so a <c>v-bind(</c> inside them never matches, and
/// balances nested parentheses to find the closing <c>)</c>. A surrounding pair of matching quotes on the
/// expression is stripped (<c>v-bind('theme.color')</c> → <c>theme.color</c>), matching
/// <c>normalizeExpression</c>. An unterminated <c>v-bind(</c> or an empty <c>v-bind()</c> reports a
/// recoverable <see cref="CssError"/> (the 2000-band catalog) located on the declaration and is left in
/// place, never throwing.
/// </para>
/// </remarks>
public static class CssBindingRewriter
{
    private const string BindingKeyword = "v-bind";

    /// <summary>
    /// Rewrites every well-formed <c>v-bind()</c> in <paramref name="stylesheet"/>'s declaration values to a
    /// custom-property reference salted by <paramref name="localHashSalt"/>.
    /// </summary>
    /// <param name="stylesheet">The parsed stylesheet to rewrite.</param>
    /// <param name="localHashSalt">The component-scoped salt (the short <c>data-v-</c> scope id).</param>
    /// <returns>The rewritten stylesheet, the collected bindings, and any malformed-usage diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stylesheet"/> or <paramref name="localHashSalt"/> is <see langword="null"/>.</exception>
    public static CssBindingRewriteResult Rewrite(CssStylesheetNode stylesheet, string localHashSalt)
    {
        if (stylesheet is null)
        {
            throw new ArgumentNullException(nameof(stylesheet));
        }

        if (localHashSalt is null)
        {
            throw new ArgumentNullException(nameof(localHashSalt));
        }

        var state = new RewriteState(localHashSalt);
        var rewritten = stylesheet with { Rules = state.RewriteRules(stylesheet.Rules) };
        return new CssBindingRewriteResult(rewritten, state.Bindings, state.Diagnostics);
    }

    // Carries the per-rewrite accumulators (salt, ordered bindings, dedupe map, diagnostics) so the
    // recursive tree walk stays a set of small pure-ish methods.
    private sealed class RewriteState
    {
        private readonly string _salt;
        private readonly Dictionary<string, string> _namesByExpression = new(StringComparer.Ordinal);

        public RewriteState(string salt) => _salt = salt;

        public List<CssVariableBinding> Bindings { get; } = [];

        public List<CssError> Diagnostics { get; } = [];

        public SyntaxList<CssSyntaxNode> RewriteRules(SyntaxList<CssSyntaxNode> rules)
        {
            var rewritten = new CssSyntaxNode[rules.Count];
            for (var index = 0; index < rules.Count; index++)
            {
                rewritten[index] = rules[index] switch
                {
                    CssQualifiedRuleNode qualified => qualified with
                    {
                        Declarations = RewriteDeclarations(qualified.Declarations),
                    },
                    // A body is nested rules (@media), declarations (@font-face), or keyframe rules
                    // (@keyframes); recursing dispatches each child by type below.
                    CssAtRuleNode atRule => atRule with { Body = RewriteRules(atRule.Body) },
                    CssKeyframeRuleNode keyframe => keyframe with
                    {
                        Declarations = RewriteDeclarations(keyframe.Declarations),
                    },
                    CssDeclarationNode declaration => RewriteDeclaration(declaration),
                    var other => other,
                };
            }

            return new SyntaxList<CssSyntaxNode>(rewritten);
        }

        private SyntaxList<CssDeclarationNode> RewriteDeclarations(SyntaxList<CssDeclarationNode> declarations)
        {
            var rewritten = new CssDeclarationNode[declarations.Count];
            for (var index = 0; index < declarations.Count; index++)
            {
                rewritten[index] = RewriteDeclaration(declarations[index]);
            }

            return new SyntaxList<CssDeclarationNode>(rewritten);
        }

        private CssDeclarationNode RewriteDeclaration(CssDeclarationNode declaration)
        {
            var value = declaration.Value;
            if (value.IndexOf(BindingKeyword, StringComparison.Ordinal) < 0)
            {
                // Fast path: no 'v-bind' substring, so nothing to rewrite (the common declaration).
                return declaration;
            }

            var rewrittenValue = RewriteValue(value, declaration.Location);
            return ReferenceEquals(rewrittenValue, value) ? declaration : declaration with { Value = rewrittenValue };
        }

        // Scans one declaration value, replacing each well-formed v-bind(expr) with var(--<hash>) and
        // collecting the binding. String literals and /* */ comments are copied verbatim so a keyword
        // inside them never matches. Returns the same instance when nothing changed.
        private string RewriteValue(string value, SourceLocation location)
        {
            StringBuilder? builder = null;
            var index = 0;
            var copiedThrough = 0;
            while (index < value.Length)
            {
                var character = value[index];
                if (character == '"' || character == '\'')
                {
                    index = SkipString(value, index);
                    continue;
                }

                if (character == '/' && index + 1 < value.Length && value[index + 1] == '*')
                {
                    index = SkipComment(value, index);
                    continue;
                }

                if (!MatchesBindingKeyword(value, index, out var afterKeyword))
                {
                    index++;
                    continue;
                }

                var open = SkipWhitespace(value, afterKeyword);
                if (open >= value.Length || value[open] != '(')
                {
                    // 'v-bind' not followed by '(' is ordinary text (e.g. a custom property named v-bind).
                    index++;
                    continue;
                }

                var argumentStart = open + 1;
                var close = LexBinding(value, argumentStart);
                if (close < 0)
                {
                    Diagnostics.Add(new CssError(CssErrorCode.UnterminatedCssBinding, location));
                    // Unrecoverable within this value: leave the remainder verbatim and stop scanning.
                    break;
                }

                var expression = NormalizeExpression(value.Substring(argumentStart, close - argumentStart));
                if (expression.Length == 0)
                {
                    Diagnostics.Add(new CssError(CssErrorCode.EmptyCssBinding, location));
                    // Leave the empty usage in place and continue past it.
                    index = close + 1;
                    continue;
                }

                builder ??= new StringBuilder(value.Length);
                builder.Append(value, copiedThrough, index - copiedThrough);
                builder.Append("var(--").Append(ResolveName(expression)).Append(')');
                index = close + 1;
                copiedThrough = index;
            }

            if (builder is null)
            {
                return value;
            }

            builder.Append(value, copiedThrough, value.Length - copiedThrough);
            return builder.ToString();
        }

        // Deduplicates by the normalized expression so a repeated v-bind(color) shares one hashed name and
        // one recorded binding (upstream vars.includes check).
        private string ResolveName(string expression)
        {
            if (_namesByExpression.TryGetValue(expression, out var name))
            {
                return name;
            }

            name = CssHash.Compute(_salt + "-" + expression);
            _namesByExpression[expression] = name;
            Bindings.Add(new CssVariableBinding(name, expression));
            return name;
        }
    }

    // Whether the literal keyword 'v-bind' sits at index; sets afterKeyword to the index just past it.
    private static bool MatchesBindingKeyword(string value, int index, out int afterKeyword)
    {
        afterKeyword = index + BindingKeyword.Length;
        return afterKeyword <= value.Length
            && string.CompareOrdinal(value, index, BindingKeyword, 0, BindingKeyword.Length) == 0;
    }

    // Finds the index of the ')' that closes the '(' whose content starts at argumentStart, balancing
    // nested parens and skipping string literals (upstream lexBinding). Returns -1 when unterminated.
    private static int LexBinding(string value, int argumentStart)
    {
        var depth = 1;
        var index = argumentStart;
        while (index < value.Length)
        {
            var character = value[index];
            if (character == '"' || character == '\'')
            {
                index = SkipString(value, index);
                continue;
            }

            if (character == '(')
            {
                depth++;
            }
            else if (character == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }

            index++;
        }

        return -1;
    }

    // Strips one pair of matching surrounding quotes (upstream normalizeExpression) after trimming.
    private static string NormalizeExpression(string raw)
    {
        var expression = raw.Trim();
        if (expression.Length >= 2)
        {
            var first = expression[0];
            if ((first == '\'' || first == '"') && expression[expression.Length - 1] == first)
            {
                return expression.Substring(1, expression.Length - 2).Trim();
            }
        }

        return expression;
    }

    // Returns the index just past a string literal that begins at index (handling backslash escapes); an
    // unterminated string consumes to end of value.
    private static int SkipString(string value, int index)
    {
        var quote = value[index];
        index++;
        while (index < value.Length)
        {
            var character = value[index];
            if (character == '\\')
            {
                index += 2;
                continue;
            }

            index++;
            if (character == quote)
            {
                break;
            }
        }

        return index;
    }

    // Returns the index just past a /* … */ comment that begins at index; an unterminated comment consumes
    // to end of value.
    private static int SkipComment(string value, int index)
    {
        index += 2;
        while (index + 1 < value.Length)
        {
            if (value[index] == '*' && value[index + 1] == '/')
            {
                return index + 2;
            }

            index++;
        }

        return value.Length;
    }

    private static int SkipWhitespace(string value, int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index;
    }
}
