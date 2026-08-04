namespace Assimalign.Viu.Tooling.UtilityCss;

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
    int SortOrder);
