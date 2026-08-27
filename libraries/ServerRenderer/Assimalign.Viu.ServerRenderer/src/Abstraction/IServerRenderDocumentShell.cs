using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Writes the host-owned document content surrounding one progressively rendered Viu root.
/// </summary>
/// <remarks>
/// The adaptor borrows the shell and never disposes it. A shell instance used by concurrent
/// requests must provide its own concurrency safety; otherwise the host supplies one instance per
/// request. The prefix runs after request-scope validation and before component execution. The
/// suffix runs only after the main render succeeds and teleport payloads are complete. Specified by
/// <c>[SSR-14]</c>.
/// </remarks>
public interface IServerRenderDocumentShell
{
    /// <summary>Writes document content that precedes the progressively rendered Viu root.</summary>
    /// <param name="output">The borrowed host response output used by the main render.</param>
    /// <param name="cancellationToken">Cancellation propagated from the host request.</param>
    /// <returns>A value task completing when the host accepts the prefix content.</returns>
    /// <remarks>
    /// The adaptor flushes non-empty unflushed prefix content before component execution. An empty
    /// prefix does not force response commitment. Specified by <c>[SSR-14]</c>.
    /// </remarks>
    ValueTask WritePrefixAsync(
        IServerRenderOutput output,
        CancellationToken cancellationToken = default);

    /// <summary>Writes document content after the Viu root and splices resolved teleports.</summary>
    /// <param name="output">The borrowed host response output used by the main render.</param>
    /// <param name="teleports">
    /// The stable target-to-payload map resolved after the main render completes.
    /// </param>
    /// <param name="cancellationToken">Cancellation propagated from the host request.</param>
    /// <returns>A value task completing when the host accepts the suffix content.</returns>
    /// <remarks>
    /// The shell chooses its own target markers and emits matching payloads verbatim. Each payload
    /// already includes the required hydration anchors. Only suffix emission points are supported;
    /// an already streamed prefix cannot be patched. The adaptor flushes non-empty unflushed suffix
    /// content. Specified by <c>[SSR-14]</c>, <c>[SSR-MARKERS-2]</c>, and <c>[HYD-6]</c>.
    /// </remarks>
    ValueTask WriteSuffixAsync(
        IServerRenderOutput output,
        IReadOnlyDictionary<string, string> teleports,
        CancellationToken cancellationToken = default);
}
