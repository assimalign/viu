namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The catalog of diagnostic codes the single-file-component block parsers emit. Unlike
/// <c>Assimalign.Viu.Syntax.Templates</c>'s <c>CompilerErrorCode</c> — whose numbering mirrors vuejs/core's
/// <c>ErrorCodes</c> — these are <b>Viu-defined</b> container diagnostics with no upstream numbering to
/// align to. Values start at 1000 to stay visibly distinct from any upstream-aligned catalog.
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
}
