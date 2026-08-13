namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// An exact source span for a detected utility candidate or scanner diagnostic.
/// </summary>
/// <param name="SourceIdentity">
/// The optional host-provided source identity, such as a normalized project-relative path.
/// </param>
/// <param name="Start">
/// The zero-based absolute start offset. A scan content offset has already been applied.
/// </param>
/// <param name="Length">The affected character count.</param>
public sealed record UtilityCandidateSourceSpan(
    string? SourceIdentity,
    int Start,
    int Length)
{
    /// <summary>
    /// Gets the exclusive absolute end offset.
    /// </summary>
    public int End => Start + Length;
}
