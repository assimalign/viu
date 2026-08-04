namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// One classified run of characters on one line of a Viu single-file component.
/// </summary>
/// <param name="LineNumber">Zero-based line number within the document.</param>
/// <param name="Start">Zero-based character offset of the run within its line.</param>
/// <param name="Length">Number of characters in the run; always greater than zero.</param>
/// <param name="ClassificationKind">What the lexer made of the run.</param>
/// <remarks>
/// Line-relative rather than absolute so the lexer never needs a line-start table, and so a caller
/// can map spans onto whatever position model its editor uses.
/// </remarks>
internal readonly record struct ViuLexicalSpan(
    int LineNumber,
    int Start,
    int Length,
    ViuClassificationKind ClassificationKind);
