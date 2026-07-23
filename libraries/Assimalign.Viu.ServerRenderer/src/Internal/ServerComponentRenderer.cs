using System;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Drives Core's shared mounted-component pipeline for one server-rendered template request.
/// </summary>
/// <remarks>
/// Setup runs in the same effect scope and live component context used by client renderers. Server
/// prefetch callbacks are awaited before rendering. Client-only mount/update/unmount lifecycle phases
/// do not run, and the temporary scope is stopped after the subtree has serialized.
/// </remarks>
internal static class ServerComponentRenderer
{
    internal static async Task RenderAsync(
        SsrRenderState state,
        ITemplateComponent request,
        ComponentContext? parent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);

        MountedComponent? instance = null;
        try
        {
            instance = MountedComponent.Create(
                state.Application,
                request,
                parent,
                state.NextComponentIdentifier());

            await instance
                .InvokeServerPrefetchAsync()
                .WaitAsync(state.CancellationToken)
                .ConfigureAwait(false);

            IComponent subtree = instance.Render();
            await ComponentTreeSerializer
                .RenderAsync(state, subtree, instance.Context)
                .ConfigureAwait(false);

            // A completed component subtree is the streaming chunk boundary. String rendering uses the
            // same call sites, where FlushAsync is intentionally a no-op.
            await state.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            // SSR does not invoke client mount/unmount hooks. AbortMount stops the component scope,
            // cancels its lifetime token, and disposes the mount-owned template.
            instance?.AbortMount();
        }
    }
}
