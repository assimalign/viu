using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Router;

/// <summary>
/// Identifies a history whose environment bridge must be initialized asynchronously before its
/// synchronous history members can be used.
/// </summary>
internal interface IInitializableRouterHistory
{
    /// <summary>Initializes the history and its environment bridge.</summary>
    /// <param name="cancellationToken">Cancels initialization.</param>
    /// <returns>A task that completes when synchronous history access is ready.</returns>
    ValueTask InitializeAsync(CancellationToken cancellationToken);
}
