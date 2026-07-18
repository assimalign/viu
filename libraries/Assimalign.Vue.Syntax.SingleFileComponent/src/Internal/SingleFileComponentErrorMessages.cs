using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.SingleFileComponent;

/// <summary>
/// The human-readable messages for each <see cref="SingleFileComponentErrorCode"/>. Kept off the public surface; the
/// parser attaches the message to each <see cref="SingleFileComponentError"/> it reports. Mirrors the shape of
/// <c>Assimalign.Vue.Syntax.Compiler</c>'s <c>CompilerErrorMessages</c>.
/// </summary>
internal static class SingleFileComponentErrorMessages
{
    private static readonly Dictionary<SingleFileComponentErrorCode, string> Messages = new()
    {
        [SingleFileComponentErrorCode.StrayTopLevelContent] =
            "Stray content outside any block. Text must live inside an @template, @script, @style, or custom block.",
        [SingleFileComponentErrorCode.MalformedBlockHeader] =
            "Malformed block header. A block opens with '@<name>' at column 0.",
        [SingleFileComponentErrorCode.MissingOpeningBrace] =
            "Block header is missing its opening '{'.",
        [SingleFileComponentErrorCode.ContentAfterOpeningBrace] =
            "Unexpected content after the opening '{'. The '{' must be the last non-whitespace character on the header line.",
        [SingleFileComponentErrorCode.MalformedOptionValue] =
            "Malformed option value. Option values must be double-quoted, e.g. lang=\"scss\".",
        [SingleFileComponentErrorCode.DuplicateTemplateBlock] =
            "Duplicate @template block. A .viu file may contain at most one @template.",
        [SingleFileComponentErrorCode.DuplicateScriptBlock] =
            "Duplicate @script block. A .viu file may contain at most one @script.",
        [SingleFileComponentErrorCode.UnterminatedBlock] =
            "Unterminated block. Expected a closing '}' at column 0 before end of file.",
    };

    /// <summary>Gets the message for <paramref name="code"/>, or an empty string when none is defined.</summary>
    /// <param name="code">The diagnostic code.</param>
    /// <returns>The human-readable message.</returns>
    public static string GetMessage(SingleFileComponentErrorCode code)
        => Messages.TryGetValue(code, out var message) ? message : string.Empty;
}
