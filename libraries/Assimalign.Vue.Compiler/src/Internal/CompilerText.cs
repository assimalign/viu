using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// The text and naming helpers the transform pipeline needs, ported from <c>@vue/shared</c>
/// (<c>general.ts</c>, <c>domAttrConfig.ts</c>) so the netstandard2.0 compiler front end does not depend on
/// the net10.0 runtime. Casing rules match upstream exactly because generated prop/handler names must agree
/// with the runtime.
/// </summary>
internal static class CompilerText
{
    private static readonly HashSet<string> BuiltInDirectives = new()
    {
        "bind", "cloak", "else-if", "else", "for", "html", "if", "model", "on", "once", "pre", "show", "slot",
        "text", "memo",
    };

    // Upstream isReservedProp's makeMap input begins with a comma, so the empty string is reserved too.
    private static readonly HashSet<string> ReservedProperties = new()
    {
        "", "key", "ref", "ref_for", "ref_key",
        "onVnodeBeforeMount", "onVnodeMounted", "onVnodeBeforeUpdate", "onVnodeUpdated",
        "onVnodeBeforeUnmount", "onVnodeUnmounted",
    };

    /// <summary>Camel-cases a hyphenated name (upstream <c>camelize</c>: <c>/-(\w)/</c> → uppercase).</summary>
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

    /// <summary>Capitalizes the first character (upstream <c>capitalize</c>).</summary>
    /// <param name="value">The value to capitalize.</param>
    public static string Capitalize(string value)
        => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);

    /// <summary>Builds an <c>onXxx</c> handler key (upstream <c>toHandlerKey</c>).</summary>
    /// <param name="value">The event name.</param>
    public static string ToHandlerKey(string value)
        => value.Length == 0 ? string.Empty : "on" + Capitalize(value);

    /// <summary>Whether <paramref name="name"/> is an event-handler key (upstream <c>isOn</c>: <c>/^on[^a-z]/</c>).</summary>
    /// <param name="name">The prop name.</param>
    public static bool IsOn(string name)
        => name.Length >= 3 && name[0] == 'o' && name[1] == 'n' && !(name[2] >= 'a' && name[2] <= 'z');

    /// <summary>Whether <paramref name="name"/> is a reserved vnode prop (upstream <c>isReservedProp</c>).</summary>
    /// <param name="name">The prop name.</param>
    public static bool IsReservedProperty(string name) => ReservedProperties.Contains(name);

    /// <summary>Whether <paramref name="name"/> is a compiler built-in directive (upstream <c>isBuiltInDirective</c>).</summary>
    /// <param name="name">The normalized directive name.</param>
    public static bool IsBuiltInDirective(string name) => BuiltInDirectives.Contains(name);

    /// <summary>
    /// Whether <paramref name="name"/> is a simple JavaScript identifier (upstream <c>isSimpleIdentifier</c>:
    /// <c>nonIdentifierRE = /^\d|[^\$\w\xA0-￿]/</c>).
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

    private static bool IsWordCharacter(char character)
        => (character >= 'a' && character <= 'z') ||
           (character >= 'A' && character <= 'Z') ||
           (character >= '0' && character <= '9') ||
           character == '_';

    // Upstream's identifier char class: $, word chars, or the U+00A0..U+FFFF range.
    private static bool IsIdentifierCharacter(char character)
        => character == '$' || IsWordCharacter(character) || character >= '\u00A0';
}
