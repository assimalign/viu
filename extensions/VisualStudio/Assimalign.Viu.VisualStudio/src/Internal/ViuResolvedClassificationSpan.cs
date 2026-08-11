namespace Assimalign.Viu.VisualStudio;

/// <summary>One editor-free span paired with its final Visual Studio classification-type name.</summary>
internal readonly record struct ViuResolvedClassificationSpan(
    int LineNumber,
    int Start,
    int Length,
    string ClassificationTypeName);
