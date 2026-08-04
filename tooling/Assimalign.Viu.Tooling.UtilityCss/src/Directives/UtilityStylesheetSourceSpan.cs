namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// An exact source span in a CSS-first project stylesheet.
/// </summary>
/// <param name="SourceIdentity">The optional host-provided source identity.</param>
/// <param name="Start">The zero-based absolute start offset.</param>
/// <param name="Length">The affected character count.</param>
public sealed record UtilityStylesheetSourceSpan(
    string? SourceIdentity,
    int Start,
    int Length)
{
    /// <summary>
    /// Gets the exclusive absolute end offset.
    /// </summary>
    public int End => Start + Length;
}
