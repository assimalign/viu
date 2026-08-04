using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Compiles class candidates into a deterministic native CSS utilities layer without invoking or
/// loading Tailwind, Node.js, PostCSS, a JavaScript configuration file, or dynamic code generation.
/// Variant behavior follows the documented
/// <see href="https://tailwindcss.com/docs/hover-focus-and-other-states">
/// Tailwind CSS v4 variant contract</see>.
/// </summary>
public static class UtilityCssCompiler
{
    /// <summary>
    /// Compiles candidates with the built-in registry and default theme.
    /// </summary>
    /// <param name="candidateTexts">The candidate set in any discovery order.</param>
    /// <returns>The generated layer, ordered rule metadata, and recoverable diagnostics.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// <paramref name="candidateTexts"/> is <see langword="null"/>.
    /// </exception>
    public static UtilityCssCompilationResult Compile(
        IEnumerable<string> candidateTexts) =>
        Compile(
            candidateTexts,
            UtilityCssRegistry.BuiltIn,
            UtilityTheme.Default,
            CancellationToken.None);

    /// <summary>
    /// Compiles candidates with explicit immutable services and cancellation.
    /// </summary>
    /// <param name="candidateTexts">The candidate set in any discovery order.</param>
    /// <param name="registry">The executable utility registry.</param>
    /// <param name="theme">The utility theme.</param>
    /// <param name="cancellationToken">The build-host cancellation boundary.</param>
    /// <returns>The generated layer, ordered rule metadata, and recoverable diagnostics.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Any required service or candidate set is <see langword="null"/>.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static UtilityCssCompilationResult Compile(
        IEnumerable<string> candidateTexts,
        UtilityCssRegistry registry,
        UtilityTheme theme,
        CancellationToken cancellationToken)
    {
        if (candidateTexts is null)
        {
            throw new ArgumentNullException(nameof(candidateTexts));
        }

        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        if (theme is null)
        {
            throw new ArgumentNullException(nameof(theme));
        }

        var rulesByCandidate = new Dictionary<string, UtilityClassMetadata>(
            StringComparer.Ordinal);
        var diagnostics = new List<UtilityCssDiagnostic>();
        foreach (var candidateText in candidateTexts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = registry.Resolve(
                candidateText,
                theme,
                cancellationToken);
            foreach (var diagnostic in result.Diagnostics)
            {
                diagnostics.Add(diagnostic);
            }

            if (result.Metadata is not null)
            {
                rulesByCandidate[result.Metadata.CandidateText] = result.Metadata;
            }
        }

        var rules = rulesByCandidate.Values
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.CandidateText, StringComparer.Ordinal)
            .ToArray();
        var css = RenderLayer(rules);
        return new UtilityCssCompilationResult(
            css,
            new UtilityCollection<UtilityClassMetadata>(rules),
            new UtilityCollection<UtilityCssDiagnostic>(diagnostics));
    }

    private static string RenderLayer(
        IEnumerable<UtilityClassMetadata> rules)
    {
        var result = new StringBuilder();
        result.AppendLine("@layer utilities {");
        foreach (var rule in rules)
        {
            foreach (var line in rule.Css.Split('\n'))
            {
                result.Append("  ");
                result.AppendLine(line.TrimEnd('\r'));
            }
        }

        result.AppendLine("}");
        return result.ToString();
    }
}
