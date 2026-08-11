using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Pairs one root render description with a host-defined per-request context.</summary>
/// <typeparam name="TContext">The host-defined request context type.</typeparam>
/// <remarks>
/// The generic context keeps framework-specific request types in downstream host code while the
/// ServerRenderer contract remains host-neutral. Specified by <c>[SSR-8]</c> and
/// <c>[SSR-11]</c>.
/// </remarks>
public sealed class ServerRenderRequest<TContext>
    where TContext : notnull
{
    /// <summary>Initializes one host render request.</summary>
    /// <param name="rootComponent">The root value in Viu's virtual-node algebra.</param>
    /// <param name="requestContext">The host-defined context for this request.</param>
    public ServerRenderRequest(VirtualNode rootComponent, TContext requestContext)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        ArgumentNullException.ThrowIfNull(requestContext);
        RootComponent = rootComponent;
        RequestContext = requestContext;
    }

    /// <summary>Gets the immutable root render description.</summary>
    public VirtualNode RootComponent { get; }

    /// <summary>Gets the host-defined per-request context.</summary>
    public TContext RequestContext { get; }
}
