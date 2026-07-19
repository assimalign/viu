namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// The catalog of recoverable diagnostic codes the CSS parser emits. Like the single-file-component
/// parser's <c>SingleFileComponentErrorCode</c>, these are <b>Vuecs-defined</b> codes: CSS Syntax Module
/// Level 3 (https://www.w3.org/TR/css-syntax-3/#error-handling) specifies recovery, not a numeric error
/// catalog, so there is no upstream numbering to pin to. Values start at 2000 to stay visibly distinct
/// from the <c>.viu</c> container catalog (1000-based) and the template compiler's upstream-aligned
/// codes.
/// </summary>
public enum CssErrorCode
{
    /// <summary>A <c>/* … */</c> comment reached end of file with no closing <c>*/</c>.</summary>
    UnterminatedComment = 2001,

    /// <summary>A quoted string reached a newline or end of file with no closing quote.</summary>
    UnterminatedString = 2002,

    /// <summary>A block opened with <c>{</c> reached end of file with no closing <c>}</c>.</summary>
    UnterminatedBlock = 2003,

    /// <summary>A <c>}</c> appeared with no matching open block and was discarded.</summary>
    UnexpectedRightBrace = 2004,

    /// <summary>A qualified rule had an empty selector prelude before its <c>{</c>.</summary>
    EmptySelector = 2005,

    /// <summary>A declaration was missing its <c>:</c> between property and value, and was discarded.</summary>
    MissingDeclarationColon = 2006,

    /// <summary>A qualified rule or at-rule reached end of file before its block opened.</summary>
    UnexpectedEndOfFile = 2007,
}
