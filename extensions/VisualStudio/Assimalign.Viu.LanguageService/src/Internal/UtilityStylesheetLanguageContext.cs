using System;
using System.Collections.Generic;

using Assimalign.Viu.Tooling.UtilityCss;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// The shared CSS-first utility configuration projected into one open component document.
/// </summary>
internal sealed class UtilityStylesheetLanguageContext
{
    private UtilityStylesheetLanguageContext(
        string stylesheetText,
        string sourceIdentity,
        UtilityStylesheetReferenceGraph referenceGraph,
        UtilitySourceConfiguration sourceConfiguration,
        UtilityTheme theme,
        UtilityProjectStylesheetCompilationOptions projectOptions,
        UtilityProjectStylesheetCompilationResult projectCompilation)
    {
        StylesheetText = stylesheetText;
        SourceIdentity = sourceIdentity;
        ReferenceGraph = referenceGraph;
        SourceConfiguration = sourceConfiguration;
        Theme = theme;
        ProjectOptions = projectOptions;
        ProjectCompilation = projectCompilation;
    }

    /// <summary>Gets the unconfigured built-in context.</summary>
    public static UtilityStylesheetLanguageContext Default { get; } =
        Create(
            string.Empty,
            string.Empty);

    /// <summary>Gets the complete authored project stylesheet.</summary>
    public string StylesheetText { get; }

    /// <summary>Gets the normalized identity of the authored project stylesheet.</summary>
    public string SourceIdentity { get; }

    /// <summary>Gets the host-resolved graph this context was built from.</summary>
    public UtilityStylesheetReferenceGraph ReferenceGraph { get; }

    /// <summary>Gets virtual-import and source behavior parsed from the stylesheet.</summary>
    public UtilitySourceConfiguration SourceConfiguration { get; }

    /// <summary>Gets the same immutable theme used by build-time generation.</summary>
    public UtilityTheme Theme { get; }

    /// <summary>Gets the direct and referenced project utility definitions.</summary>
    public UtilityProjectStylesheetCompilationResult ProjectCompilation { get; }

    private UtilityProjectStylesheetCompilationOptions ProjectOptions { get; }

    /// <summary>Creates a context for one project stylesheet.</summary>
    public static UtilityStylesheetLanguageContext Create(
        string? stylesheetText,
        string sourceIdentity,
        UtilityStylesheetReferenceGraph? referenceGraph = null)
    {
        var css = stylesheetText ?? string.Empty;
        var sourceConfiguration =
            UtilitySourceParser.Parse(css).Configuration;
        var theme = UtilityThemeParser.Parse(
            css,
            new UtilityThemeParseOptions
            {
                Prefix = sourceConfiguration.Prefix,
                ImportedThemeOptions =
                    sourceConfiguration.ImportedThemeOptions,
                IsImportant = sourceConfiguration.IsImportant,
                SourceIdentity = sourceIdentity,
            }).Theme;
        var projectOptions =
            new UtilityProjectStylesheetCompilationOptions
            {
                SourceIdentity = sourceIdentity,
                Theme = theme,
                ReferenceGraph =
                    referenceGraph ??
                    UtilityStylesheetReferenceGraph.Empty,
            };
        var projectCompilation =
            UtilityProjectStylesheetCompiler.Compile(
                css,
                Array.Empty<string>(),
                projectOptions);
        return new UtilityStylesheetLanguageContext(
            css,
            sourceIdentity,
            projectOptions.ReferenceGraph,
            sourceConfiguration,
            theme,
            projectOptions,
            projectCompilation);
    }

    /// <summary>
    /// Determines whether this context was already built from the supplied host inputs.
    /// </summary>
    /// <param name="stylesheetText">The authored project stylesheet, or null.</param>
    /// <param name="sourceIdentity">The normalized stylesheet identity.</param>
    /// <param name="referenceGraph">The host-resolved reference graph.</param>
    /// <returns><see langword="true"/> when recreating the context would be redundant.</returns>
    public bool Matches(
        string? stylesheetText,
        string sourceIdentity,
        UtilityStylesheetReferenceGraph referenceGraph) =>
        string.Equals(
            StylesheetText,
            stylesheetText ?? string.Empty,
            StringComparison.Ordinal) &&
        string.Equals(
            SourceIdentity,
            sourceIdentity,
            StringComparison.Ordinal) &&
        ReferenceEquals(ReferenceGraph, referenceGraph);

    /// <summary>Compiles editor candidates through project utility and variant definitions.</summary>
    public UtilityProjectStylesheetCompilationResult Compile(
        string candidate) =>
        Compile(new[] { candidate });

    /// <summary>Compiles an editor candidate set in one deterministic project pass.</summary>
    public UtilityProjectStylesheetCompilationResult Compile(
        IEnumerable<string> candidates) =>
        UtilityProjectStylesheetCompiler.Compile(
            StylesheetText,
            candidates,
            ProjectOptions);
}
