using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// The human-readable messages for each <see cref="CssErrorCode"/>. Kept off the public surface; the
/// parser attaches the message to each <see cref="CssError"/> it reports. Mirrors the shape of the
/// single-file-component parser's <c>SingleFileComponentErrorMessages</c>.
/// </summary>
internal static class CssErrorMessages
{
    private static readonly Dictionary<CssErrorCode, string> Messages = new Dictionary<CssErrorCode, string>
    {
        [CssErrorCode.UnterminatedComment] =
            "Unterminated comment. Expected a closing '*/' before end of file.",
        [CssErrorCode.UnterminatedString] =
            "Unterminated string. Expected a closing quote before the line break or end of file.",
        [CssErrorCode.UnterminatedBlock] =
            "Unterminated block. Expected a closing '}' before end of file.",
        [CssErrorCode.UnexpectedRightBrace] =
            "Unexpected '}' with no matching open block; the brace was discarded.",
        [CssErrorCode.EmptySelector] =
            "Empty selector. A style rule must have a selector before its '{'.",
        [CssErrorCode.MissingDeclarationColon] =
            "Malformed declaration. Expected ':' between the property and its value; the declaration was discarded.",
        [CssErrorCode.UnexpectedEndOfFile] =
            "Unexpected end of file. Expected a '{' block or ';' to close the rule.",
    };

    /// <summary>Gets the message for <paramref name="code"/>, or an empty string when none is defined.</summary>
    /// <param name="code">The diagnostic code.</param>
    /// <returns>The human-readable message.</returns>
    public static string GetMessage(CssErrorCode code)
        => Messages.TryGetValue(code, out var message) ? message : string.Empty;
}
