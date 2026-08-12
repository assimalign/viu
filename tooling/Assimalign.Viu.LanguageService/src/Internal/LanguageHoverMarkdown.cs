using System;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Formats one hover the way a tooltip reads: the declaration first, then the prose about it.
/// </summary>
/// <remarks>
/// <para>
/// The declaration is fenced, and the fence carries its language. That language is the only thing
/// telling a reader of this content which grammar the declaration is written in, and a host that
/// colorizes the tooltip needs it — the same body serves a C# signature and a CSS rule.
/// </para>
/// <para>
/// A fence is a document construct, so a host that renders Markdown directly draws it as an artifact
/// and decorates it with a copy button. That is a rendering decision rather than a content one: the
/// Visual Studio adapter builds the tooltip from classified runs instead of handing the Markdown
/// over, which is what both colorizes the declaration and leaves the decoration off.
/// </para>
/// <para>
/// Prose follows the declaration, unemphasized, so it renders at ordinary body size. Heading and
/// list markers would render at heading and list sizes, which is what made a Viu tooltip read as
/// something other than the editor's own.
/// </para>
/// </remarks>
internal static class LanguageHoverMarkdown
{
    /// <summary>The fence language a C# declaration carries.</summary>
    internal const string CSharpLanguage = "csharp";

    /// <summary>The fence language a CSS declaration carries.</summary>
    internal const string CssLanguage = "css";

    /// <summary>Formats a C# declaration and the prose describing it.</summary>
    /// <param name="declaration">The declaration, which may span lines.</param>
    /// <param name="description">The prose, or empty for a declaration on its own.</param>
    /// <returns>The hover Markdown.</returns>
    internal static string Create(string declaration, string description)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(description);

        var fence = CreateFence(CSharpLanguage, declaration);
        return description.Length == 0 ? fence : fence + "\n" + description;
    }

    /// <summary>Formats prose followed by the CSS declaration it refers to.</summary>
    /// <param name="description">The prose introducing the declaration.</param>
    /// <param name="declaration">The declaration, which may span lines.</param>
    /// <returns>The hover Markdown.</returns>
    internal static string CreateDescribed(string description, string declaration)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(declaration);

        return declaration.Length == 0
            ? description
            : description + "\n\n" + CreateFence(CssLanguage, declaration);
    }

    private static string CreateFence(string language, string declaration)
        => "```" + language + "\n" + declaration.TrimEnd('\n', '\r') + "\n```";
}
