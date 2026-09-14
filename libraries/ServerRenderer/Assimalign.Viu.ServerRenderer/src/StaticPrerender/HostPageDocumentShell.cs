using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Surrounds a rendered root with the unchanged content of the application's published host page.
/// </summary>
/// <remarks>
/// The prefix ends immediately after the mount element's opening tag; the suffix begins at its
/// matching closing tag. Existing mount children are discarded. Asset references, bootstrap scripts,
/// whitespace, and attribute spelling outside those children remain unchanged. Instances are immutable
/// and may be shared across independent renders. This shell rejects teleport output because it has no
/// authored teleport insertion points; use a custom <see cref="IServerRenderDocumentShell"/> when
/// teleports are required. Specified by <c>[SSG-3]</c> and <c>[SSR-14]</c>.
/// </remarks>
public sealed class HostPageDocumentShell : IServerRenderDocumentShell
{
    private readonly ReadOnlyMemory<char> _prefix;
    private readonly ReadOnlyMemory<char> _suffix;

    /// <summary>Finds one mount container in published HTML without rewriting its surrounding text.</summary>
    /// <param name="hostPage">The complete published host-page text, including resolved asset references.</param>
    /// <param name="mountSelector">
    /// An identifier selector: <c>#</c>, an ASCII letter or underscore, then ASCII letters, digits,
    /// underscores, or hyphens. General CSS selectors and escapes are deliberately unsupported.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The selector is unsupported, the target is missing or duplicated, its closing tag is missing,
    /// or it is a void, self-closing, template, or raw-text element instead of a mount container.
    /// </exception>
    /// <remarks>
    /// Identifier matching is case-sensitive; HTML tag and attribute names are case-insensitive.
    /// Comments, raw-text contents, and template contents do not contain eligible mount targets.
    /// The host page must use a correctly nested mount with an explicit matching closing tag;
    /// this source scanner does not apply a browser's HTML error recovery. Specified by <c>[SSG-3]</c>.
    /// </remarks>
    public HostPageDocumentShell(string hostPage, string mountSelector = "#app")
    {
        ArgumentNullException.ThrowIfNull(hostPage);
        ArgumentNullException.ThrowIfNull(mountSelector);
        (int contentStart, int contentEnd) = HostPageMountParser.Find(hostPage, mountSelector);
        _prefix = hostPage.AsMemory(0, contentStart);
        _suffix = hostPage.AsMemory(contentEnd);
    }

    /// <summary>Writes the exact host-page prefix through the mount container's opening tag.</summary>
    /// <param name="output">The borrowed destination; this method does not flush or dispose it.</param>
    /// <param name="cancellationToken">Cancellation propagated from the render request.</param>
    /// <returns>A value task completing when the destination accepts the prefix.</returns>
    /// <remarks>Specified by <c>[SSG-3]</c> and <c>[SSR-14]</c>.</remarks>
    public ValueTask WritePrefixAsync(
        IServerRenderOutput output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        cancellationToken.ThrowIfCancellationRequested();
        return output.WriteAsync(_prefix, cancellationToken);
    }

    /// <summary>Writes the exact host-page suffix beginning at the mount container's closing tag.</summary>
    /// <param name="output">The borrowed destination; this method does not flush or dispose it.</param>
    /// <param name="teleports">The completed teleport map, which must be empty for this shell.</param>
    /// <param name="cancellationToken">Cancellation propagated from the render request.</param>
    /// <returns>A value task completing when the destination accepts the suffix.</returns>
    /// <exception cref="NotSupportedException">The render produced teleport output.</exception>
    /// <remarks>
    /// Rejecting teleports prevents silently emitting a document missing hydration targets.
    /// Specified by <c>[SSG-3]</c>, <c>[SSR-14]</c>, and <c>[HYD-6]</c>.
    /// </remarks>
    public ValueTask WriteSuffixAsync(
        IServerRenderOutput output,
        IReadOnlyDictionary<string, string> teleports,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(teleports);
        cancellationToken.ThrowIfCancellationRequested();
        if (teleports.Count != 0)
        {
            throw new NotSupportedException(
                "The host-page document shell cannot place teleport output. "
                + "Provide an IServerRenderDocumentShell with explicit teleport targets.");
        }

        return output.WriteAsync(_suffix, cancellationToken);
    }
}
