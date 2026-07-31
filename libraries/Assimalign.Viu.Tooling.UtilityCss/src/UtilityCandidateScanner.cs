using System;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Detects complete literal Viu Utilities v4.3.3 candidates in supplied template or host-markup
/// text. The scanner is deterministic, I/O-free, and treats the supplied region as plain text
/// without evaluating its programming or template language. The cursor helper separately limits
/// editor completion to static <c>class</c> values and literal or object-key regions in
/// <c>:class</c>/<c>v-bind:class</c> values.
/// </summary>
/// <remarks>
/// Candidate boundary behavior follows Tailwind CSS v4's plain-text extraction contract:
/// <see href="https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/crates/oxide/src/extractor/candidate_machine.rs"/>.
/// </remarks>
public static class UtilityCandidateScanner
{
    /// <summary>
    /// Scans all plain text in <paramref name="text"/> with the built-in variant registry and
    /// source-relative spans.
    /// </summary>
    /// <param name="text">The supplied template or host-markup text.</param>
    /// <returns>The recoverable per-source scan result.</returns>
    public static UtilityCandidateScanResult Scan(string? text) =>
        Scan(text, null, CancellationToken.None);

    /// <summary>
    /// Scans all plain text in <paramref name="text"/> with explicit host context and cancellation.
    /// </summary>
    /// <param name="text">The supplied template or host-markup text.</param>
    /// <param name="options">
    /// Optional source identity, absolute content offset, configured prefix, and variant registry.
    /// </param>
    /// <param name="cancellationToken">The cancellation boundary for editor and build hosts.</param>
    /// <returns>The recoverable per-source scan result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The configured content offset is negative.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static UtilityCandidateScanResult Scan(
        string? text,
        UtilityCandidateScanOptions? options,
        CancellationToken cancellationToken = default) =>
        UtilityCandidateScanEngine.Scan(
            text,
            options ?? UtilityCandidateScanOptions.Default,
            cancellationToken);

    /// <summary>
    /// Finds the balanced class token surrounding <paramref name="position"/> using
    /// source-relative spans.
    /// </summary>
    /// <param name="text">The supplied template or host-markup text.</param>
    /// <param name="position">
    /// The zero-based cursor position in <paramref name="text"/>, between zero and its length.
    /// </param>
    /// <returns>
    /// The token context, including an empty token between classes, or <see langword="null"/> when
    /// the cursor is not in a supported class value.
    /// </returns>
    public static UtilityCandidateToken? FindTokenAtPosition(
        string? text,
        int position) =>
        FindTokenAtPosition(
            text,
            position,
            null,
            CancellationToken.None);

    /// <summary>
    /// Finds the balanced class token surrounding <paramref name="position"/> with explicit host
    /// context and cancellation.
    /// </summary>
    /// <param name="text">The supplied template or host-markup text.</param>
    /// <param name="position">
    /// The zero-based cursor position in <paramref name="text"/>, between zero and its length.
    /// </param>
    /// <param name="options">Optional source identity and absolute content offset.</param>
    /// <param name="cancellationToken">The cancellation boundary for editor hosts.</param>
    /// <returns>
    /// The token context, including an empty token between classes, or <see langword="null"/> when
    /// the cursor is outside a supported class value or a runtime-generated fragment.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The configured content offset is negative.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static UtilityCandidateToken? FindTokenAtPosition(
        string? text,
        int position,
        UtilityCandidateScanOptions? options,
        CancellationToken cancellationToken = default) =>
        UtilityCandidateScanEngine.FindTokenAtPosition(
            text,
            position,
            options ?? UtilityCandidateScanOptions.Default,
            cancellationToken);

    /// <summary>
    /// Determines whether <paramref name="position"/> falls inside a markup attribute value.
    /// </summary>
    /// <param name="text">The supplied template or host-markup text.</param>
    /// <param name="position">
    /// The zero-based cursor position in <paramref name="text"/>, between zero and its length.
    /// </param>
    /// <param name="cancellationToken">The cancellation boundary for editor hosts.</param>
    /// <returns>
    /// <see langword="true"/> when the cursor is inside any attribute value, including the directive,
    /// event, and binding values this scanner does not treat as class contexts; otherwise
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Editor hosts pair this with <see cref="FindTokenAtPosition(string?, int)"/>: a null token
    /// together with a true result means the cursor sits in an attribute value that carries no utility
    /// candidate, which must not fall back to markup-name completion.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static bool IsInsideAttributeValue(
        string? text,
        int position,
        CancellationToken cancellationToken = default) =>
        UtilityCandidateScanEngine.IsInsideAttributeValue(
            text,
            position,
            cancellationToken);
}
