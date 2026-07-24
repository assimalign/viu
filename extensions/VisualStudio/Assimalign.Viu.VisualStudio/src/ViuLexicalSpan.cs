namespace Assimalign.Viu.VisualStudio;

internal readonly record struct ViuLexicalSpan(
    int LineNumber,
    int Start,
    int Length,
    ViuClassificationKind ClassificationKind);
