namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Immutable host context for one I/O-free CSS-first theme parse.
/// </summary>
public sealed record UtilityThemeParseOptions
{
    /// <summary>
    /// Gets the default parse options, which extend <see cref="UtilityTheme.Default"/>.
    /// </summary>
    public static UtilityThemeParseOptions Default { get; } = new();

    /// <summary>
    /// Gets the semantic base theme extended or reset by the supplied CSS.
    /// </summary>
    public UtilityTheme BaseTheme { get; init; } = UtilityTheme.Default;

    /// <summary>
    /// Gets an optional lowercase import-level prefix applied before authored <c>@theme</c> blocks
    /// are parsed. A conflicting authored prefix produces the normal located theme diagnostic.
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Gets import-level behavior applied to the inherited base theme before authored
    /// <c>@theme</c> declarations are evaluated. Only <see cref="UtilityThemeOptions.Inline"/> and
    /// <see cref="UtilityThemeOptions.Static"/> are valid here.
    /// </summary>
    public UtilityThemeOptions ImportedThemeOptions { get; init; }

    /// <summary>
    /// Gets whether the virtual utility import enables globally important generated declarations.
    /// </summary>
    public bool IsImportant { get; init; }

    /// <summary>
    /// Gets the optional source identity copied onto declarations and diagnostics.
    /// </summary>
    public string? SourceIdentity { get; init; }

    /// <summary>
    /// Gets the non-negative absolute offset added to every source-relative span.
    /// </summary>
    public int ContentOffset { get; init; }
}
