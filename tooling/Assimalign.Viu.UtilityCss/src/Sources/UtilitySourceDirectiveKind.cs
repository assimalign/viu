namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Identifies one CSS-first source inclusion or exclusion.
/// </summary>
public enum UtilitySourceDirectiveKind
{
    /// <summary>A filesystem source root added with <c>@source "..."</c>.</summary>
    IncludePath,

    /// <summary>A filesystem source root removed with <c>@source not "..."</c>.</summary>
    ExcludePath,

    /// <summary>Candidates added with <c>@source inline("...")</c>.</summary>
    IncludeInline,

    /// <summary>Candidates removed with <c>@source not inline("...")</c>.</summary>
    ExcludeInline,
}
