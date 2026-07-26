using System;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Parses the CSS-first utility directives and functions supported by Viu Utilities.
/// </summary>
/// <remarks>
/// The syntax boundary is pinned to Tailwind CSS v4.3.3's
/// <see href="https://tailwindcss.com/docs/functions-and-directives">functions and directives</see>
/// and
/// <see href="https://tailwindcss.com/docs/adding-custom-styles#adding-custom-utilities">
/// custom utility</see> documentation. This parser is an independent, I/O-free implementation; it
/// does not load Tailwind CSS, JavaScript configuration, or plugins.
/// </remarks>
public static class UtilityStylesheetParser
{
    /// <summary>
    /// Parses <paramref name="css"/> with default source context.
    /// </summary>
    /// <param name="css">The project stylesheet content.</param>
    /// <returns>The immutable, recoverable parse result.</returns>
    public static UtilityStylesheetParseResult Parse(string? css) =>
        Parse(
            css,
            null,
            CancellationToken.None);

    /// <summary>
    /// Parses <paramref name="css"/> with explicit source and cancellation context.
    /// </summary>
    /// <param name="css">The project stylesheet content.</param>
    /// <param name="options">Optional source identity and absolute content offset.</param>
    /// <param name="cancellationToken">The build or editor cancellation boundary.</param>
    /// <returns>The immutable, recoverable parse result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The configured content offset is negative.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static UtilityStylesheetParseResult Parse(
        string? css,
        UtilityStylesheetParseOptions? options,
        CancellationToken cancellationToken = default) =>
        UtilityStylesheetParseEngine.Parse(
            css,
            options ?? UtilityStylesheetParseOptions.Default,
            cancellationToken);
}
