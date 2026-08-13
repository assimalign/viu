namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// An exact source span in CSS-first theme input.
/// </summary>
/// <param name="SourceIdentity">The optional host-provided source identity.</param>
/// <param name="Start">The zero-based absolute start offset after applying the content offset.</param>
/// <param name="Length">The affected character count.</param>
public sealed record UtilityThemeSourceSpan(
    string? SourceIdentity,
    int Start,
    int Length)
{
    /// <summary>
    /// Gets the exclusive absolute end offset.
    /// </summary>
    public int End => Start + Length;
}
