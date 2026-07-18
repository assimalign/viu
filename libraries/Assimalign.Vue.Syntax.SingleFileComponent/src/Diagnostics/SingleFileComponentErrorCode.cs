namespace Assimalign.Vue.Syntax.SingleFileComponent;

/// <summary>
/// The catalog of diagnostic codes the <c>.viu</c> block parser emits. Unlike
/// <c>Assimalign.Vue.Syntax.Compiler</c>'s <c>CompilerErrorCode</c> — whose numbering mirrors vuejs/core's
/// <c>ErrorCodes</c> — these are <b>Vuecs-defined</b> codes with no upstream counterpart: the
/// <c>@name { }</c> container is a Vuecs divergence, so its structural diagnostics have no vuejs/core
/// numbering to align to. Values start at 1000 to stay visibly distinct from any upstream-aligned
/// catalog.
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

    /// <summary>A file declared more than one <c>@template</c> block.</summary>
    DuplicateTemplateBlock = 1006,

    /// <summary>A file declared more than one <c>@script</c> block.</summary>
    DuplicateScriptBlock = 1007,

    /// <summary>A block was opened but reached end of file with no column-0 closing <c>}</c>.</summary>
    UnterminatedBlock = 1008,
}
