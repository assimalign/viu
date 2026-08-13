using System;
using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Executes Viu Utilities CSS-first customization without file access, JavaScript, or Tailwind.
/// </summary>
/// <remarks>
/// Functional utilities and composition follow the independent v4.3.3-compatible contract described
/// by the official
/// <see href="https://tailwindcss.com/docs/adding-custom-styles#functional-utilities">
/// functional utility</see>,
/// <see href="https://tailwindcss.com/docs/adding-custom-styles#adding-custom-variants">
/// custom variant</see>, and
/// <see href="https://tailwindcss.com/docs/functions-and-directives">directive</see>
/// documentation.
/// </remarks>
public static class UtilityProjectStylesheetCompiler
{
    /// <summary>
    /// Compiles a project stylesheet using built-in services and no references.
    /// </summary>
    /// <param name="css">The authored project stylesheet.</param>
    /// <param name="candidateTexts">The discovered candidate set.</param>
    /// <returns>The rewritten stylesheet and generated custom utility rules.</returns>
    public static UtilityProjectStylesheetCompilationResult Compile(
        string? css,
        IEnumerable<string> candidateTexts) =>
        Compile(
            css,
            candidateTexts,
            null,
            CancellationToken.None);

    /// <summary>
    /// Compiles a project stylesheet with explicit immutable services and cancellation.
    /// </summary>
    /// <param name="css">The authored project stylesheet.</param>
    /// <param name="candidateTexts">The discovered candidate set.</param>
    /// <param name="options">Optional source, theme, registry, and reference-graph context.</param>
    /// <param name="cancellationToken">The build or editor cancellation boundary.</param>
    /// <returns>The rewritten stylesheet and generated custom utility rules.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="candidateTexts"/> or an explicitly configured service is null.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static UtilityProjectStylesheetCompilationResult Compile(
        string? css,
        IEnumerable<string> candidateTexts,
        UtilityProjectStylesheetCompilationOptions? options,
        CancellationToken cancellationToken = default)
    {
        if (candidateTexts is null)
        {
            throw new ArgumentNullException(nameof(candidateTexts));
        }

        var effectiveOptions = options ?? new UtilityProjectStylesheetCompilationOptions();
        if (effectiveOptions.Registry is null)
        {
            throw new ArgumentNullException(nameof(options), "The utility registry cannot be null.");
        }

        if (effectiveOptions.Theme is null)
        {
            throw new ArgumentNullException(nameof(options), "The utility theme cannot be null.");
        }

        if (effectiveOptions.ReferenceGraph is null)
        {
            throw new ArgumentNullException(nameof(options), "The reference graph cannot be null.");
        }

        return UtilityProjectStylesheetCompileEngine.Compile(
            css ?? string.Empty,
            candidateTexts,
            effectiveOptions,
            cancellationToken);
    }
}
