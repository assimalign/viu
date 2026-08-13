using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The text and naming helpers the transform pipeline needs. They live here, not in a shared runtime
/// assembly, because the compiler front end targets netstandard2.0 to load inside Roslyn and must not
/// depend on the net10.0 runtime. The casing rules are a <b>frozen contract</b>: the compiler derives a
/// prop or handler name here, and the runtime must derive the identical name from the same input, so a
/// change on either side is a change to both.
/// </summary>
internal static class CompilerText
{
    private static readonly HashSet<string> BuiltInDirectives = new()
    {
        "bind", "cloak", "else-if", "else", "for", "html", "if", "model", "on", "once", "pre", "show", "slot",
        "text", "memo",
    };

    // The empty string counts as reserved, so a nameless prop never reaches prop building.
    private static readonly HashSet<string> ReservedProperties = new()
    {
        "", "key", "ref", "ref_for", "ref_key",
        "onVnodeBeforeMount", "onVnodeMounted", "onVnodeBeforeUpdate", "onVnodeUpdated",
        "onVnodeBeforeUnmount", "onVnodeUnmounted",
    };

    /// <summary>Camel-cases a hyphenated name: each <c>-x</c> becomes <c>X</c>.</summary>
    /// <param name="value">The name to camel-case.</param>
    public static string Camelize(string value)
    {
        if (value.IndexOf('-') < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '-' && index + 1 < value.Length && IsWordCharacter(value[index + 1]))
            {
                builder.Append(char.ToUpperInvariant(value[index + 1]));
                index++;
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    /// <summary>Capitalizes the first character.</summary>
    /// <param name="value">The value to capitalize.</param>
    public static string Capitalize(string value)
        => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);

    /// <summary>Builds an <c>onXxx</c> handler key.</summary>
    /// <param name="value">The event name.</param>
    public static string ToHandlerKey(string value)
        => value.Length == 0 ? string.Empty : "on" + Capitalize(value);

    /// <summary>Whether <paramref name="name"/> is an event-handler key (<c>on</c> followed by a non-lowercase character).</summary>
    /// <param name="name">The prop name.</param>
    public static bool IsOn(string name)
        => name.Length >= 3 && name[0] == 'o' && name[1] == 'n' && !(name[2] >= 'a' && name[2] <= 'z');

    /// <summary>Whether <paramref name="name"/> is a reserved vnode prop.</summary>
    /// <param name="name">The prop name.</param>
    public static bool IsReservedProperty(string name) => ReservedProperties.Contains(name);

    /// <summary>Whether <paramref name="name"/> is a compiler built-in directive.</summary>
    /// <param name="name">The normalized directive name.</param>
    public static bool IsBuiltInDirective(string name) => BuiltInDirectives.Contains(name);

    /// <summary>
    /// Whether <paramref name="name"/> is a simple identifier \u2014 it does not start with a digit and
    /// contains only <c>$</c>, word characters, or U+00A0..U+FFFF.
    /// </summary>
    /// <param name="name">The candidate identifier.</param>
    public static bool IsSimpleIdentifier(string name)
    {
        if (name.Length == 0)
        {
            return true;
        }

        if (name[0] >= '0' && name[0] <= '9')
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!IsIdentifierCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Replaces complete identifier tokens without changing longer authored identifiers that merely contain
    /// <paramref name="identifier"/>.
    /// </summary>
    /// <param name="text">The text containing identifier tokens.</param>
    /// <param name="identifier">The complete compiler-owned identifier to replace.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="replaceMemberAccess">
    /// Whether an identifier immediately following a member-access dot may be replaced.
    /// </param>
    /// <returns>The original text when no complete token matches; otherwise, the rewritten text.</returns>
    public static string ReplaceIdentifierToken(
        string text,
        string identifier,
        string replacement,
        bool replaceMemberAccess = true)
    {
        var searchStart = 0;
        var copyStart = 0;
        StringBuilder? builder = null;
        while (searchStart < text.Length)
        {
            var index = text.IndexOf(identifier, searchStart, System.StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            var end = index + identifier.Length;
            var hasIdentifierBefore = index > 0 &&
                (IsIdentifierCharacter(text[index - 1]) || text[index - 1] == '@');
            var hasIdentifierAfter = end < text.Length && IsIdentifierCharacter(text[end]);
            var isMemberAccess = index > 0 && text[index - 1] == '.';
            if (!hasIdentifierBefore && !hasIdentifierAfter && (replaceMemberAccess || !isMemberAccess))
            {
                builder ??= new StringBuilder(text.Length + replacement.Length);
                builder.Append(text, copyStart, index - copyStart);
                builder.Append(replacement);
                copyStart = end;
            }

            searchStart = end;
        }

        if (builder is null)
        {
            return text;
        }

        builder.Append(text, copyStart, text.Length - copyStart);
        return builder.ToString();
    }

    private static bool IsWordCharacter(char character)
        => (character >= 'a' && character <= 'z') ||
           (character >= 'A' && character <= 'Z') ||
           (character >= '0' && character <= '9') ||
           character == '_';

    // The identifier character class: $, word chars, or the U+00A0..U+FFFF range.
    private static bool IsIdentifierCharacter(char character)
        => character == '$' || IsWordCharacter(character) || character >= '\u00A0';
}
