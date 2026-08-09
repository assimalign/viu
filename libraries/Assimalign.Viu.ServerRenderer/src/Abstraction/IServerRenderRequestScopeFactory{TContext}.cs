using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Creates a fresh, owned server-render scope for each typed host request.</summary>
/// <typeparam name="TContext">The host-defined request context type.</typeparam>
/// <remarks>
/// This is the structural per-request boundary: services, component registrations, and state may
/// be composed from the host context without exposing that host's framework types to Viu.
/// Specified by <c>[SSR-8]</c>, <c>[SSR-9]</c>, and <c>[SSR-11]</c>.
/// </remarks>
public interface IServerRenderRequestScopeFactory<TContext>
    where TContext : notnull
{
    /// <summary>Creates the fresh scope used only for the supplied request.</summary>
    /// <param name="request">The root component and host-defined request context.</param>
    /// <param name="cancellationToken">Cancellation propagated from the host request.</param>
    /// <returns>The request-owned scope that the adaptor will dispose.</returns>
    ValueTask<IServerRenderRequestScope> CreateAsync(
        ServerRenderRequest<TContext> request,
        CancellationToken cancellationToken = default);
}
