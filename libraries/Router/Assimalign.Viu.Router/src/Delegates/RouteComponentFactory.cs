using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// Resolves one lazy route's immutable component request during navigation without activating a
/// component or bypassing the application-owned component factory.
/// </summary>
/// <param name="cancellationToken">
/// Cancels the resolve attempt when its navigation is superseded or cancelled.
/// </param>
/// <returns>The typed component request to retain after a successful resolve.</returns>
/// <remarks>
/// The router caches only a successful result. A failure is routed through
/// <see cref="Router.OnError(NavigationErrorHandler)"/> and a later navigation invokes the factory
/// again. Specified by <c>[V01.01.08.05]</c>.
/// </remarks>
public delegate Task<ComponentNode> RouteComponentFactory(
    CancellationToken cancellationToken);
