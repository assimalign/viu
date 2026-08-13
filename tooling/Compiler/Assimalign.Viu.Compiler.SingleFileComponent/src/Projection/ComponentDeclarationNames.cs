using System;
using System.Text;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// Derives a component parameter's or event's canonical template-facing name from the C# member name
/// that declares it, and classifies a declared C# type for the build-time compatibility check. Both
/// rules are purely lexical, so the build host and the editor derive the same name from the same
/// member without a semantic model. Specified by <c>[CMP-27]</c>.
/// </summary>
public static class ComponentDeclarationNames
{
    /// <summary>
    /// The camel-case spelling of <paramref name="memberName"/>: the leading run of upper-case letters
    /// is lower-cased, except that a run longer than one keeps its last letter capitalized when a
    /// lower-case letter follows it. <c>Title</c> → <c>title</c>, <c>ModelValue</c> →
    /// <c>modelValue</c>, <c>URL</c> → <c>url</c>, <c>HTMLContent</c> → <c>htmlContent</c>. The runtime
    /// additionally accepts the kebab-case spelling of the result [CMP-13], so no separate kebab
    /// derivation is needed here.
    /// </summary>
    /// <param name="memberName">The declaring property or method name.</param>
    /// <returns>The canonical argument or event name.</returns>
    public static string Derive(string memberName)
    {
        if (memberName.Length == 0 || !char.IsUpper(memberName[0]))
        {
            return memberName;
        }

        var builder = new StringBuilder(memberName.Length);
        var index = 0;
        while (index < memberName.Length && char.IsUpper(memberName[index]))
        {
            // Keep the last letter of a multi-letter upper-case run capitalized when it starts the
            // next word ("HTMLContent" -> "htmlContent"), so the derived name reads as one identifier.
            if (index > 0 &&
                index + 1 < memberName.Length &&
                char.IsLower(memberName[index + 1]))
            {
                break;
            }

            builder.Append(char.ToLowerInvariant(memberName[index]));
            index++;
        }

        builder.Append(memberName, index, memberName.Length - index);
        return builder.ToString();
    }

    /// <summary>
    /// The comparison key for an argument name: its kebab-case spelling folded back to camel case, so a
    /// template's <c>model-value</c> and a declaration's <c>modelValue</c> compare equal. This mirrors
    /// the alias set Core builds for every declared parameter [CMP-13], so a name matches here exactly
    /// when it would resolve at mount.
    /// </summary>
    /// <param name="name">The authored or declared name.</param>
    /// <returns>The comparison key.</returns>
    public static string Canonicalize(string name)
    {
        if (name.IndexOf('-') < 0)
        {
            return name;
        }

        var builder = new StringBuilder(name.Length);
        var uppercaseNext = false;
        foreach (var character in name)
        {
            if (character == '-')
            {
                uppercaseNext = true;
                continue;
            }

            builder.Append(uppercaseNext ? char.ToUpperInvariant(character) : character);
            uppercaseNext = false;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Classifies a declared C# type spelling for the build-time compatibility check. Only the
    /// predefined keyword spellings are decided; every other spelling — a named type, an alias, a
    /// generic instantiation — stays <see cref="ComponentValueKind.Unknown"/>, because the projection
    /// has no semantic model and a wrong decision would be a false positive.
    /// </summary>
    /// <param name="typeText">The declared type exactly as spelled in the <c>@script</c> block.</param>
    /// <returns>The decidable classification, or <see cref="ComponentValueKind.Unknown"/>.</returns>
    public static ComponentValueKind ClassifyTypeText(string typeText)
    {
        var text = typeText.Trim();
        if (text.EndsWith("?", StringComparison.Ordinal))
        {
            text = text.Substring(0, text.Length - 1).TrimEnd();
        }

        return text switch
        {
            "string" => ComponentValueKind.Text,
            "object" or "dynamic" => ComponentValueKind.Any,
            "bool" or "byte" or "sbyte" or "char" or "decimal" or "double" or "float"
                or "int" or "uint" or "long" or "ulong" or "short" or "ushort"
                or "nint" or "nuint" => ComponentValueKind.Value,
            _ => ComponentValueKind.Unknown,
        };
    }
}
