using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>
/// Loads the registered identity of an asynchronous component.
/// </summary>
/// <remarks>
/// The loader returns an identity, not an activated template. Core continues to activate the
/// resolved template through the application-owned component factory. The cancellation token is
/// canceled when every mount sharing the in-flight load has unmounted. This contract mirrors Vue
/// 3.5's async-component loader while remaining trimming safe:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/apiAsyncComponent.ts.
/// </remarks>
/// <param name="cancellationToken">Cancels the shared load when it no longer has mounted consumers.</param>
/// <returns>The registered template identity produced by the load.</returns>
public delegate Task<AsynchronousComponentTarget> AsynchronousComponentLoader(
    CancellationToken cancellationToken);
