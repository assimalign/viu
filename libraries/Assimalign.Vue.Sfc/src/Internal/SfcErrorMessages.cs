using System.Collections.Generic;

namespace Assimalign.Vue.Sfc;

/// <summary>
/// The human-readable messages for each <see cref="SfcErrorCode"/>. Kept off the public surface; the
/// parser attaches the message to each <see cref="SfcError"/> it reports. Mirrors the shape of
/// <c>Assimalign.Vue.Compiler</c>'s <c>CompilerErrorMessages</c>.
/// </summary>
internal static class SfcErrorMessages
{
    private static readonly Dictionary<SfcErrorCode, string> Messages = new()
    {
        [SfcErrorCode.StrayTopLevelContent] =
            "Stray content outside any block. Text must live inside an @template, @script, @style, or custom block.",
        [SfcErrorCode.MalformedBlockHeader] =
            "Malformed block header. A block opens with '@<name>' at column 0.",
        [SfcErrorCode.MissingOpeningBrace] =
            "Block header is missing its opening '{'.",
        [SfcErrorCode.ContentAfterOpeningBrace] =
            "Unexpected content after the opening '{'. The '{' must be the last non-whitespace character on the header line.",
        [SfcErrorCode.MalformedOptionValue] =
            "Malformed option value. Option values must be double-quoted, e.g. lang=\"scss\".",
        [SfcErrorCode.DuplicateTemplateBlock] =
            "Duplicate @template block. A .viu file may contain at most one @template.",
        [SfcErrorCode.DuplicateScriptBlock] =
            "Duplicate @script block. A .viu file may contain at most one @script.",
        [SfcErrorCode.UnterminatedBlock] =
            "Unterminated block. Expected a closing '}' at column 0 before end of file.",
    };

    /// <summary>Gets the message for <paramref name="code"/>, or an empty string when none is defined.</summary>
    /// <param name="code">The diagnostic code.</param>
    /// <returns>The human-readable message.</returns>
    public static string GetMessage(SfcErrorCode code)
        => Messages.TryGetValue(code, out var message) ? message : string.Empty;
}
