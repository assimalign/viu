namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// The container section the lexer is currently inside.
/// </summary>
/// <remarks>
/// A Viu single-file component is a sequence of sections, and a line's classification depends
/// entirely on which one encloses it — the same text colors differently in a template, in
/// <c>@script</c>, and in a style block. This is the whole of the lexer's cross-line state.
/// </remarks>
internal enum ViuSectionKind
{
    /// <summary>Top-level text between sections.</summary>
    None,

    /// <summary>A <c>&lt;template&gt;</c> tag section or a legacy <c>@template</c> block.</summary>
    Template,

    /// <summary>An <c>@script</c> block.</summary>
    Script,

    /// <summary>A <c>&lt;style&gt;</c> tag section or a legacy <c>@style</c> block.</summary>
    Style,
}
