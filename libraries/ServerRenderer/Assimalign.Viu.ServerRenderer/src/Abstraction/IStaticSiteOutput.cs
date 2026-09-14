using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Publishes complete static documents without prescribing a storage provider.</summary>
/// <remarks>The generator borrows this output. Failed renders never reach it. Specified by <c>[SSG-2]</c> and <c>[SSG-5]</c>.</remarks>
public interface IStaticSiteOutput
{
    /// <summary>Writes one complete UTF-8 document and returns its emitted storage location.</summary>
    /// <param name="relativePath">The validated, slash-separated document path under the output root.</param>
    /// <param name="document">The completed host page, rendered tree, and any state island.</param>
    /// <param name="cancellationToken">Cancellation for the write.</param>
    /// <returns>The emitted location, reported only after the write succeeds.</returns>
    ValueTask<string> WriteAsync(string relativePath, string document, CancellationToken cancellationToken = default);
}
