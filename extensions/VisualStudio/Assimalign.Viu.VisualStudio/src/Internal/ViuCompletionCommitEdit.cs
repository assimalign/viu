namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// One completion commit expressed against a line: what to replace, what to write, and where the
/// caret lands.
/// </summary>
/// <remarks>
/// Editor-free by design, in the same shape as <see cref="ViuAutoClosingEdit"/>, so the decision that
/// produces it can be unit-tested without an editor and the adapter that applies it stays a
/// translation.
/// </remarks>
internal sealed class ViuCompletionCommitEdit
{
    /// <summary>Initializes a commit edit.</summary>
    /// <param name="replaceStart">Zero-based offset within the line where the replacement begins.</param>
    /// <param name="replaceLength">Number of characters the replacement consumes.</param>
    /// <param name="text">The text written in their place.</param>
    /// <param name="caretOffset">Where the caret lands, relative to <paramref name="replaceStart"/>.</param>
    internal ViuCompletionCommitEdit(
        int replaceStart,
        int replaceLength,
        string text,
        int caretOffset)
    {
        this.ReplaceStart = replaceStart;
        this.ReplaceLength = replaceLength;
        this.Text = text;
        this.CaretOffset = caretOffset;
    }

    /// <summary>Gets the zero-based offset within the line where the replacement begins.</summary>
    public int ReplaceStart { get; }

    /// <summary>Gets the number of characters the replacement consumes.</summary>
    public int ReplaceLength { get; }

    /// <summary>Gets the text written in their place.</summary>
    public string Text { get; }

    /// <summary>Gets where the caret lands, relative to <see cref="ReplaceStart"/>.</summary>
    public int CaretOffset { get; }
}
