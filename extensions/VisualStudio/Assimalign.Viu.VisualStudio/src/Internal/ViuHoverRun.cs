namespace Assimalign.Viu.VisualStudio;

/// <summary>One run of hover text and the classification that colors it.</summary>
/// <remarks>
/// Editor-free, so the decision about what a tooltip is made of stays unit-testable and the Visual
/// Studio adapter that draws it is a translation. The classification name is the same currency
/// <see cref="ViuClassificationTypeNames"/> deals in, so a hover and the document behind it are
/// colored from one table.
/// </remarks>
internal readonly struct ViuHoverRun
{
    /// <summary>Initializes a run.</summary>
    /// <param name="classificationTypeName">The classification type that colors the text.</param>
    /// <param name="text">The text of the run.</param>
    internal ViuHoverRun(string classificationTypeName, string text)
    {
        this.ClassificationTypeName = classificationTypeName;
        this.Text = text;
    }

    /// <summary>Gets the classification type that colors the text.</summary>
    public string ClassificationTypeName { get; }

    /// <summary>Gets the text of the run.</summary>
    public string Text { get; }
}
