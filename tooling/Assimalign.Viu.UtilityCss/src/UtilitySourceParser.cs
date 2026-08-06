using System;
using System.Threading;

namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Parses the Viu-owned virtual utility import and Tailwind-v4.3.3-compatible CSS-first source
/// directives without filesystem access.
/// </summary>
/// <remarks>
/// Source detection behavior follows the official
/// <see href="https://tailwindcss.com/docs/detecting-classes-in-source-files">
/// Tailwind CSS v4.3 source-detection contract</see>. The recognized import specifier is
/// <c>viu-utilities</c>; importing Tailwind is rejected so this feature remains standalone.
/// </remarks>
public static class UtilitySourceParser
{
    /// <summary>
    /// Parses source configuration with the default context.
    /// </summary>
    /// <param name="css">The project utility stylesheet.</param>
    /// <returns>The recoverable source-configuration result.</returns>
    public static UtilitySourceParseResult Parse(string? css) =>
        Parse(
            css,
            null,
            CancellationToken.None);

    /// <summary>
    /// Parses source configuration with explicit source, expansion, and cancellation context.
    /// </summary>
    /// <param name="css">The project utility stylesheet.</param>
    /// <param name="options">Optional source and expansion context.</param>
    /// <param name="cancellationToken">The build or editor cancellation boundary.</param>
    /// <returns>The recoverable source-configuration result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The content offset is negative or the expansion limit is not positive.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static UtilitySourceParseResult Parse(
        string? css,
        UtilitySourceParseOptions? options,
        CancellationToken cancellationToken = default) =>
        UtilitySourceParseEngine.Parse(
            css,
            options ?? UtilitySourceParseOptions.Default,
            cancellationToken);
}
