namespace Assimalign.Viu.VisualStudio;

/// <summary>One exact C# classification at an authored document range.</summary>
internal readonly record struct ViuSemanticClassification(
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter,
    string ClassificationTypeName)
{
    /// <summary>Gets whether the range is a non-empty single-line editor span.</summary>
    internal bool IsValid =>
        StartLine >= 0 &&
        StartCharacter >= 0 &&
        EndLine == StartLine &&
        EndCharacter > StartCharacter &&
        !string.IsNullOrWhiteSpace(ClassificationTypeName);
}
