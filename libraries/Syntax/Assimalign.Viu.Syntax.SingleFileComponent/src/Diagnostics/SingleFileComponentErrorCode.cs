namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The catalog of diagnostic codes the single-file-component block parsers emit. Values start at 1000
/// so a container diagnostic is distinguishable at a glance from
/// <c>Assimalign.Viu.Syntax.Templates</c>'s <c>CompilerErrorCode</c>, whose parse band occupies the low
/// numbers: the two catalogs are separate enums that a build surfaces side by side, and a reader must be
/// able to tell which stage reported a code without consulting either enum. Since the
/// [V01.01.06.10] hybrid container, the tag codes (1009–1013) — originally minted for the <c>.vue</c>
/// compatibility parser — are also reachable from <c>.viu</c> files, whose canonical
/// <c>&lt;template&gt;</c>/<c>&lt;style&gt;</c> blocks are tag-based. Each code's severity comes from
/// the catalog (<c>SingleFileComponentErrorMessages.GetSeverity</c>): the legacy-container codes
/// (1015/1016) are warnings; everything else is an error.
/// </summary>
public enum SingleFileComponentErrorCode
{
    /// <summary>Non-whitespace text appeared at the top level, outside any block.</summary>
    StrayTopLevelContent = 1001,

    /// <summary>A top-level line began with <c>@</c> but no valid block name followed.</summary>
    MalformedBlockHeader = 1002,

    /// <summary>A block header named a block but had no opening <c>{</c> on its line.</summary>
    MissingOpeningBrace = 1003,

    /// <summary>Non-whitespace followed the opening <c>{</c> on a block header line.</summary>
    ContentAfterOpeningBrace = 1004,

    /// <summary>A block option value was not a well-formed double-quoted string.</summary>
    MalformedOptionValue = 1005,

    /// <summary>A file declared more than one template block.</summary>
    DuplicateTemplateBlock = 1006,

    /// <summary>A file declared more than one ordinary script block.</summary>
    DuplicateScriptBlock = 1007,

    /// <summary>A block was opened but reached end of file with no column-0 closing <c>}</c>.</summary>
    UnterminatedBlock = 1008,

    /// <summary>A tag-based top-level block did not have a valid opening tag.</summary>
    MalformedTagBlock = 1009,

    /// <summary>An attribute on a tag-based top-level block was malformed.</summary>
    MalformedTagAttribute = 1010,

    /// <summary>A tag-based file contained a top-level closing tag without an opening block.</summary>
    UnexpectedClosingTag = 1011,

    /// <summary>A tag-based top-level block reached end of file without its matching closing tag.</summary>
    UnterminatedTagBlock = 1012,

    /// <summary>A tag-based top-level block declared the same attribute more than once.</summary>
    DuplicateTagAttribute = 1013,

    /// <summary>A tag-based file declared more than one <c>&lt;script setup&gt;</c> block.</summary>
    DuplicateScriptSetupBlock = 1014,

    /// <summary>
    /// A <c>.viu</c> file used the legacy <c>@template { }</c> container; the canonical container is the
    /// <c>&lt;template&gt;</c> tag ([V01.01.06.10]). Warning severity — the block still parses during the
    /// migration window.
    /// </summary>
    LegacyTemplateBlockSyntax = 1015,

    /// <summary>
    /// A <c>.viu</c> file used the legacy <c>@style … { }</c> container; the canonical container is the
    /// <c>&lt;style&gt;</c> tag ([V01.01.06.10]). Warning severity — the block still parses during the
    /// migration window.
    /// </summary>
    LegacyStyleBlockSyntax = 1016,

    /// <summary>
    /// A <c>.viu</c> file declared a top-level <c>&lt;script&gt;</c> tag. A <c>.viu</c> component's C#
    /// lives in <c>@script { }</c>; a tag-based script contributes no block and its content is never
    /// compiled or executed.
    /// </summary>
    ScriptTagBlockNotSupported = 1017,
}
