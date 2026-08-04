using System;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Parses top-level Tailwind-v4.3.3-compatible <c>@theme</c> blocks into an immutable Viu theme
/// overlay. Parsing is deterministic, recoverable, and I/O-free.
/// </summary>
/// <remarks>
/// The compatibility reference is
/// <see href="https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/theme.ts">
/// Tailwind CSS v4.3.3 theme behavior</see>. This implementation does not load or depend on
/// Tailwind CSS.
/// </remarks>
public static class UtilityThemeParser
{
    /// <summary>
    /// Parses <paramref name="css"/> as an overlay on <see cref="UtilityTheme.Default"/>.
    /// </summary>
    /// <param name="css">CSS that may contain top-level <c>@theme</c> blocks.</param>
    /// <returns>The recoverable theme parse result.</returns>
    public static UtilityThemeParseResult Parse(string? css) =>
        Parse(
            css,
            null,
            CancellationToken.None);

    /// <summary>
    /// Parses <paramref name="css"/> with explicit base-theme, source, offset, and cancellation
    /// context.
    /// </summary>
    /// <param name="css">CSS that may contain top-level <c>@theme</c> blocks.</param>
    /// <param name="options">Optional semantic base theme and source context.</param>
    /// <param name="cancellationToken">The build or editor cancellation boundary.</param>
    /// <returns>The recoverable theme parse result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The configured content offset is negative.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static UtilityThemeParseResult Parse(
        string? css,
        UtilityThemeParseOptions? options,
        CancellationToken cancellationToken = default) =>
        UtilityThemeParseEngine.Parse(
            css,
            options ?? UtilityThemeParseOptions.Default,
            cancellationToken);
}
