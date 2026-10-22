namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// The token categories the <see cref="CssTokenizer"/> produces, following the CSS Syntax Module Level 3
/// tokenizer output (https://www.w3.org/TR/css-syntax-3/#tokenization). The set is trimmed to what
/// rule-level parsing and scoped-selector rewriting need: numeric variants (number/percentage/dimension)
/// collapse into <see cref="Number"/> because the rule parser keeps raw value slices rather than typed
/// numerics, and the <c>url()</c>/unicode-range refinements are not modeled separately.
/// </summary>
internal enum CssTokenKind
{
    /// <summary>A run of whitespace (<c>\t\n\r\f</c> and space) — a <c>&lt;whitespace-token&gt;</c>.</summary>
    Whitespace,

    /// <summary>A <c>/* … */</c> comment. Preserved as a token so spans stay exact; dropped when serializing.</summary>
    Comment,

    /// <summary>An identifier — an <c>&lt;ident-token&gt;</c> (e.g. <c>div</c>, <c>red</c>, <c>color</c>).</summary>
    Ident,

    /// <summary>An identifier immediately followed by <c>(</c> — a <c>&lt;function-token&gt;</c> (e.g. <c>deep(</c>, <c>calc(</c>).</summary>
    Function,

    /// <summary>An <c>@</c> followed by an identifier — an <c>&lt;at-keyword-token&gt;</c> (e.g. <c>@media</c>, <c>@keyframes</c>).</summary>
    AtKeyword,

    /// <summary>A <c>#</c> followed by name characters — a <c>&lt;hash-token&gt;</c> (e.g. <c>#id</c>, a color).</summary>
    Hash,

    /// <summary>A quoted string — a <c>&lt;string-token&gt;</c>.</summary>
    String,

    /// <summary>A numeric token — number, percentage, or dimension collapsed together.</summary>
    Number,

    /// <summary>A single code point that is not part of any other token — a <c>&lt;delim-token&gt;</c> (e.g. <c>.</c>, <c>*</c>, <c>&gt;</c>).</summary>
    Delim,

    /// <summary>A <c>:</c> — a <c>&lt;colon-token&gt;</c>.</summary>
    Colon,

    /// <summary>A <c>;</c> — a <c>&lt;semicolon-token&gt;</c>.</summary>
    Semicolon,

    /// <summary>A <c>,</c> — a <c>&lt;comma-token&gt;</c>.</summary>
    Comma,

    /// <summary>A <c>{</c> — a <c>&lt;{-token&gt;</c>.</summary>
    LeftBrace,

    /// <summary>A <c>}</c> — a <c>&lt;}-token&gt;</c>.</summary>
    RightBrace,

    /// <summary>A <c>(</c> — a <c>&lt;(-token&gt;</c>.</summary>
    LeftParenthesis,

    /// <summary>A <c>)</c> — a <c>&lt;)-token&gt;</c>.</summary>
    RightParenthesis,

    /// <summary>A <c>[</c> — a <c>&lt;[-token&gt;</c>.</summary>
    LeftBracket,

    /// <summary>A <c>]</c> — a <c>&lt;]-token&gt;</c>.</summary>
    RightBracket,

    /// <summary>The end of the source — an <c>&lt;EOF-token&gt;</c>.</summary>
    EndOfFile,
}
