using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Router;

/// <summary>
/// Extends a router history whose environment bridge must become ready asynchronously before its
/// synchronous history members can be used. A <see cref="Router"/> detects this capability and
/// awaits it from <see cref="Router.ReadyAsync(CancellationToken)"/> before reading the initial
/// location. Specified by <c>[RTR-3]</c> and <c>[RTR-7]</c>.
/// </summary>
/// <remarks>
/// The capability is separate from <see cref="IRouterHistory"/> so pure histories do not manufacture
/// asynchronous work. Implementations remain single-threaded, matching the router's host model.
/// </remarks>
public interface IInitializableRouterHistory
{
    /// <summary>
    /// Initializes the history's environment bridge. Completion guarantees that every synchronous
    /// <see cref="IRouterHistory"/> member is ready for use; cancellation or failure leaves readiness
    /// incomplete and propagates through <see cref="Router.ReadyAsync(CancellationToken)"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels initialization and the initial navigation that awaits it.</param>
    /// <returns>A task that completes when synchronous history access is ready.</returns>
    ValueTask InitializeAsync(CancellationToken cancellationToken);
}
