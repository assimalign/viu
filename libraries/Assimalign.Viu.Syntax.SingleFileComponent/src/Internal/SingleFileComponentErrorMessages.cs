using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The human-readable messages and severities for each <see cref="SingleFileComponentErrorCode"/>. Kept off the
/// public surface; the parser attaches the message and catalog severity to each
/// <see cref="SingleFileComponentError"/> it reports. Mirrors the shape of
/// <c>Assimalign.Viu.Syntax.Templates</c>'s <c>CompilerErrorMessages</c>.
/// </summary>
internal static class SingleFileComponentErrorMessages
{
    private static readonly Dictionary<SingleFileComponentErrorCode, string> Messages = new()
    {
        [SingleFileComponentErrorCode.StrayTopLevelContent] =
            "Stray content outside any single-file-component block.",
        [SingleFileComponentErrorCode.MalformedBlockHeader] =
            "Malformed block header. A block opens with '@<name>' at column 0.",
        [SingleFileComponentErrorCode.MissingOpeningBrace] =
            "Block header is missing its opening '{'.",
        [SingleFileComponentErrorCode.ContentAfterOpeningBrace] =
            "Unexpected content after the opening '{'. The '{' must be the last non-whitespace character on the header line.",
        [SingleFileComponentErrorCode.MalformedOptionValue] =
            "Malformed option value. Option values must be double-quoted, e.g. lang=\"scss\".",
        [SingleFileComponentErrorCode.DuplicateTemplateBlock] =
            "Duplicate template block. A single-file component may contain at most one template block.",
        [SingleFileComponentErrorCode.DuplicateScriptBlock] =
            "Duplicate script block. A single-file component may contain at most one script block.",
        [SingleFileComponentErrorCode.UnterminatedBlock] =
            "Unterminated block. Expected a closing '}' at column 0 before end of file.",
        [SingleFileComponentErrorCode.MalformedTagBlock] =
            "Malformed tag-based block. Expected a complete top-level opening tag.",
        [SingleFileComponentErrorCode.MalformedTagAttribute] =
            "Malformed top-level block attribute. Expected a name with an optional quoted or unquoted value.",
        [SingleFileComponentErrorCode.UnexpectedClosingTag] =
            "Unexpected top-level closing tag without a corresponding opening block.",
        [SingleFileComponentErrorCode.UnterminatedTagBlock] =
            "Unterminated tag-based block. Expected a matching closing tag before end of file.",
        [SingleFileComponentErrorCode.DuplicateTagAttribute] =
            "Duplicate attribute on a top-level tag-based block.",
        [SingleFileComponentErrorCode.DuplicateScriptSetupBlock] =
            "Duplicate script setup block. A .vue single-file component may contain at most one script setup block.",
        [SingleFileComponentErrorCode.LegacyTemplateBlockSyntax] =
            "The '@template { }' block container is legacy syntax and will be removed. Rewrite the block as '<template>...</template>'; block options become tag attributes (for example lang=\"html\").",
        [SingleFileComponentErrorCode.LegacyStyleBlockSyntax] =
            "The '@style { }' block container is legacy syntax and will be removed. Rewrite the block as '<style>...</style>'; block options become tag attributes (for example '<style scoped>' or '<style module=\"classes\">').",
        [SingleFileComponentErrorCode.ScriptTagBlockNotSupported] =
            "A top-level '<script>' tag is not supported in a .viu file and its content is never compiled or executed. Declare the component's C# with '@script { }'.",
    };

    /// <summary>Gets the message for <paramref name="code"/>, or an empty string when none is defined.</summary>
    /// <param name="code">The diagnostic code.</param>
    /// <returns>The human-readable message.</returns>
    public static string GetMessage(SingleFileComponentErrorCode code)
        => Messages.TryGetValue(code, out var message) ? message : string.Empty;

    /// <summary>
    /// Gets the catalog severity for <paramref name="code"/>: the [V01.01.06.10] legacy-container codes
    /// (<see cref="SingleFileComponentErrorCode.LegacyTemplateBlockSyntax"/> /
    /// <see cref="SingleFileComponentErrorCode.LegacyStyleBlockSyntax"/>) are warnings — the blocks still
    /// parse during the migration window — and every other code is a recoverable error, mirroring
    /// <c>@vue/compiler-sfc</c>'s <c>parse().errors</c>.
    /// </summary>
    /// <param name="code">The diagnostic code.</param>
    /// <returns>The severity every diagnostic reported with <paramref name="code"/> carries.</returns>
    public static DiagnosticSeverity GetSeverity(SingleFileComponentErrorCode code)
        => code switch
        {
            SingleFileComponentErrorCode.LegacyTemplateBlockSyntax => DiagnosticSeverity.Warning,
            SingleFileComponentErrorCode.LegacyStyleBlockSyntax => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Error,
        };
}
