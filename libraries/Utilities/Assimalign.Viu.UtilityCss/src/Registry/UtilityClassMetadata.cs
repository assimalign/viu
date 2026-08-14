namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Shared compiler and editor metadata for one resolved utility class.
/// </summary>
/// <param name="CandidateText">The complete class candidate.</param>
/// <param name="Description">A concise hover and completion description.</param>
/// <param name="Css">The complete deterministic CSS rule, including variant wrappers.</param>
/// <param name="SortOrder">The registry-defined deterministic output order.</param>
public sealed record UtilityClassMetadata(
    string CandidateText,
    string Description,
    string Css,
    int SortOrder)
{
    /// <summary>
    /// Gets the single unambiguous resolved CSS color value when the candidate is color-bearing, or
    /// <see langword="null"/> when it has no color or yields multiple distinct color values. The
    /// value is resolved from structured candidate and theme data so editor consumers never need
    /// to infer colors by scanning <see cref="Css"/>.
    /// </summary>
    public string? ColorValue { get; init; }
}
