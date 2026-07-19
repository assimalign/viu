using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The set of CSS Modules accessors ([V01.01.05.04.01]) a template's expressions may reference, keyed by their
/// C#-parseable spelling. Supplied by the composition-root generator ([V01.01.06.06]) alongside
/// <see cref="BindingMetadata"/>, it lets expression classification resolve <c>$style.box</c> (and named-module
/// forms) to the generated accessor class rather than a phantom component binding — the Vuecs stand-in for Vue
/// 3.5's runtime <c>$style</c> render-context object, which Vuecs cannot have because it generates static,
/// trimming-safe C# (no runtime proxy, no dynamic member access).
/// </summary>
/// <remarks>
/// Transform <i>input</i> like <see cref="BindingMetadata"/>: a plain immutable class rebuilt from the generator's
/// value-equatable module-class map, so it never rides in the cached model and the incremental cache stays intact.
/// The <c>$</c> in <c>$style</c> is not a legal C# identifier character, so an expression is parsed against the
/// length-preserving <c>$</c>→<c>_</c> substitution (<see cref="Substitute"/>) — the same spelling-substitution
/// precedent as <c>$event</c>→<c>__event</c> and <c>$slots</c>→<c>__slots</c>.
/// </remarks>
public sealed class CssModuleAccessors
{
    /// <summary>The empty set — no CSS module accessors are in scope (the common, no-<c>module</c> component).</summary>
    public static readonly CssModuleAccessors Empty = new(Array.Empty<CssModuleAccessor>(), reportsUnknownMembers: false);

    private readonly Dictionary<string, CssModuleAccessor> byParseIdentifier;
    private readonly List<KeyValuePair<string, string>> substitutions;

    /// <summary>Creates a CSS module accessor set.</summary>
    /// <param name="accessors">The accessors in scope.</param>
    /// <param name="reportsUnknownMembers">
    /// Whether a member access on an accessor whose name is not a declared class surfaces a
    /// <see cref="CompilerErrorCode.XVuecsUnknownCssModuleMember"/> diagnostic. The generator sets this because it
    /// supplies the <i>complete</i> class map (every declared class), so an unknown member is decidably wrong;
    /// defaults to <see langword="false"/> so a partial map never spuriously errors.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="accessors"/> is <see langword="null"/>.</exception>
    public CssModuleAccessors(IEnumerable<CssModuleAccessor> accessors, bool reportsUnknownMembers)
    {
        if (accessors is null)
        {
            throw new ArgumentNullException(nameof(accessors));
        }

        byParseIdentifier = new Dictionary<string, CssModuleAccessor>(StringComparer.Ordinal);
        substitutions = new List<KeyValuePair<string, string>>();
        foreach (var accessor in accessors)
        {
            byParseIdentifier[accessor.ParseIdentifier] = accessor;
            if (!string.Equals(accessor.TemplateName, accessor.ParseIdentifier, StringComparison.Ordinal))
            {
                // A spelling that is not already C#-parseable (the `$style` default): record its
                // length-preserving substitution so Substitute() can rewrite it before the Roslyn parse.
                substitutions.Add(new KeyValuePair<string, string>(accessor.TemplateName, accessor.ParseIdentifier));
            }
        }

        ReportsUnknownMembers = reportsUnknownMembers;
    }

    /// <summary>The number of accessors in scope.</summary>
    public int Count => byParseIdentifier.Count;

    /// <summary>Whether an access to an undeclared member surfaces a diagnostic (the generator supplies a complete map).</summary>
    public bool ReportsUnknownMembers { get; }

    /// <summary>Resolves the accessor for a C#-parseable identifier (the <see cref="Substitute"/>d spelling).</summary>
    /// <param name="parseIdentifier">The identifier as it appears in the parsed expression.</param>
    /// <param name="accessor">The resolved accessor when found.</param>
    /// <returns><see langword="true"/> when <paramref name="parseIdentifier"/> names an accessor.</returns>
    public bool TryResolve(string parseIdentifier, out CssModuleAccessor accessor)
        => byParseIdentifier.TryGetValue(parseIdentifier, out accessor!);

    /// <summary>
    /// Rewrites the not-yet-C#-parseable accessor spellings in <paramref name="raw"/> to their parse identifiers
    /// (<c>$style</c>→<c>_style</c>), at identifier boundaries and length-preserving so every other offset in the
    /// expression is unchanged. A no-op when no <c>$</c>-spelling is in scope or <paramref name="raw"/> has no
    /// <c>$</c>.
    /// </summary>
    /// <param name="raw">The raw expression text.</param>
    /// <returns>The substituted text, or the same instance when nothing changed.</returns>
    public string Substitute(string raw)
    {
        if (substitutions.Count == 0 || raw.IndexOf('$') < 0)
        {
            return raw;
        }

        var result = raw;
        foreach (var pair in substitutions)
        {
            result = ReplaceToken(result, pair.Key, pair.Value);
        }

        return result;
    }

    // Replaces whole-token occurrences of `from` with `to`. A match counts only when the character just past it
    // is not an identifier-continuation char, so a longer name is never partially rewritten; the leading char is
    // never an identifier-continuation char for a `$`-prefixed token, so only the trailing boundary is checked.
    private static string ReplaceToken(string text, string from, string to)
    {
        var index = text.IndexOf(from, StringComparison.Ordinal);
        if (index < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var cursor = 0;
        while (index >= 0)
        {
            var after = index + from.Length;
            builder.Append(text, cursor, index - cursor);
            if (after >= text.Length || !IsIdentifierContinuation(text[after]))
            {
                builder.Append(to);
            }
            else
            {
                builder.Append(from);
            }

            cursor = after;
            index = text.IndexOf(from, cursor, StringComparison.Ordinal);
        }

        builder.Append(text, cursor, text.Length - cursor);
        return builder.ToString();
    }

    private static bool IsIdentifierContinuation(char character)
        => (character >= 'a' && character <= 'z') ||
           (character >= 'A' && character <= 'Z') ||
           (character >= '0' && character <= '9') ||
           character == '_';
}
