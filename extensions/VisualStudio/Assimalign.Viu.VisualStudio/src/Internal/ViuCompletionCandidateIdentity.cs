using System;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Identifies one completion candidate using the fields preserved by Visual Studio's LSP adapter.
/// </summary>
internal readonly struct ViuCompletionCandidateIdentity :
    IEquatable<ViuCompletionCandidateIdentity>
{
    /// <summary>Initializes a normalized completion candidate identity.</summary>
    internal ViuCompletionCandidateIdentity(
        string displayText,
        string insertText,
        string sortText,
        string filterText)
    {
        this.DisplayText = displayText ?? throw new ArgumentNullException(nameof(displayText));
        this.InsertText = insertText ?? throw new ArgumentNullException(nameof(insertText));
        this.SortText = sortText ?? throw new ArgumentNullException(nameof(sortText));
        this.FilterText = filterText ?? throw new ArgumentNullException(nameof(filterText));
    }

    /// <summary>Gets the candidate's displayed label.</summary>
    internal string DisplayText { get; }

    /// <summary>Gets the text committed by the candidate.</summary>
    internal string InsertText { get; }

    /// <summary>Gets the candidate's sorting identity.</summary>
    internal string SortText { get; }

    /// <summary>Gets the candidate's filtering identity.</summary>
    internal string FilterText { get; }

    /// <inheritdoc />
    public bool Equals(ViuCompletionCandidateIdentity other) =>
        string.Equals(this.DisplayText, other.DisplayText, StringComparison.Ordinal) &&
        string.Equals(this.InsertText, other.InsertText, StringComparison.Ordinal) &&
        string.Equals(this.SortText, other.SortText, StringComparison.Ordinal) &&
        string.Equals(this.FilterText, other.FilterText, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? value) =>
        value is ViuCompletionCandidateIdentity other && this.Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hash = StringComparer.Ordinal.GetHashCode(this.DisplayText);
        hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(this.InsertText);
        hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(this.SortText);
        return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(this.FilterText);
    }
}
