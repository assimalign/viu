using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Renders Viu's host-neutral virtual tree to WHATWG-serialized HTML without a browser, DOM, or
/// JavaScript interop boundary.
/// </summary>
/// <remarks>
/// Each call owns its write state and component render leases and selects fresh logical Core,
/// Reactivity, State, and scheduler execution state. Distinct request-owned application graphs may
/// therefore render concurrently, but an individual graph remains single-event-loop and must not be
/// shared. Applications should be composed per request when their borrowed services or state are
/// request-scoped. Specified by <c>[EXE-1]</c> and <c>[SSR-1]</c> through <c>[SSR-10]</c>.
/// </remarks>
public static class ServerRenderer
{
    /// <summary>Renders one configured server application to an HTML string.</summary>
    /// <param name="application">The immutable per-render application composition.</param>
    /// <param name="context">The per-render state handoff context, or null to create one.</param>
    /// <param name="cancellationToken">Cancellation for component prefetch and rendering.</param>
    /// <returns>The completed WHATWG HTML serialization.</returns>
    /// <remarks>Specified by <c>[SSR-1]</c>, <c>[SSR-2]</c>, and <c>[SSR-6]</c>.</remarks>
    public static async Task<string> RenderToStringAsync(
        ServerRenderApplication application,
        SsrContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        SsrWriter writer = new();
        await RenderCoreAsync(
            application,
            writer,
            context ?? new SsrContext(),
            cancellationToken).ConfigureAwait(false);
        return writer.ToStringResult();
    }

    /// <summary>
    /// Renders one virtual tree without separately configuring component registrations or services.
    /// </summary>
    /// <param name="rootComponent">The root value in Viu's closed virtual-node algebra.</param>
    /// <param name="context">The per-render state handoff context, or null to create one.</param>
    /// <param name="cancellationToken">Cancellation for rendering.</param>
    /// <returns>The completed WHATWG HTML serialization.</returns>
    /// <remarks>
    /// A tree containing <see cref="ComponentNode"/> values must instead use a
    /// <see cref="ServerRenderApplication"/> with registrations for those values. Specified by
    /// <c>[SSR-1]</c>, <c>[SSR-3]</c>, and <c>[SSR-6]</c>.
    /// </remarks>
    public static Task<string> RenderToStringAsync(
        VirtualNode rootComponent,
        SsrContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        return RenderToStringAsync(
            CreatePrimitiveApplication(rootComponent),
            context,
            cancellationToken);
    }

    /// <summary>
    /// Streams one configured server application, flushing completed component subtrees so the
    /// destination controls backpressure.
    /// </summary>
    /// <param name="application">The immutable per-render application composition.</param>
    /// <param name="writer">The externally owned destination writer.</param>
    /// <param name="context">The per-render state handoff context, or null to create one.</param>
    /// <param name="cancellationToken">Cancellation for prefetch, writes, and flushes.</param>
    /// <returns>A task that completes after all produced content has been flushed.</returns>
    /// <remarks>Specified by <c>[SSR-1]</c>, <c>[SSR-4]</c>, and <c>[SSR-10]</c>.</remarks>
    public static Task RenderToStreamAsync(
        ServerRenderApplication application,
        TextWriter writer,
        SsrContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(writer);
        return RenderCoreAsync(
            application,
            new SsrWriter(writer),
            context ?? new SsrContext(),
            cancellationToken);
    }

    /// <summary>
    /// Streams one virtual tree without separately configuring component registrations or services.
    /// </summary>
    /// <param name="rootComponent">The root value in Viu's closed virtual-node algebra.</param>
    /// <param name="writer">The externally owned destination writer.</param>
    /// <param name="context">The per-render state handoff context, or null to create one.</param>
    /// <param name="cancellationToken">Cancellation for writes and flushes.</param>
    /// <returns>A task that completes after all produced content has been flushed.</returns>
    /// <remarks>Specified by <c>[SSR-1]</c>, <c>[SSR-3]</c>, and <c>[SSR-6]</c>.</remarks>
    public static Task RenderToStreamAsync(
        VirtualNode rootComponent,
        TextWriter writer,
        SsrContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        return RenderToStreamAsync(
            CreatePrimitiveApplication(rootComponent),
            writer,
            context,
            cancellationToken);
    }

    private static ServerRenderApplication CreatePrimitiveApplication(VirtualNode rootComponent)
    {
        ApplicationOptions options = new()
        {
            RootComponent = rootComponent,
        };
        return new ServerRenderApplication(new ApplicationContext(options));
    }

    private static async Task RenderCoreAsync(
        ServerRenderApplication application,
        SsrWriter writer,
        SsrContext context,
        CancellationToken cancellationToken)
    {
        using IDisposable executionIsolation = CoreExecutionIsolation.Enter();
        cancellationToken.ThrowIfCancellationRequested();
        IApplicationContext applicationContext = application.Context;
        ServerRenderStatePayload.PrepareContext(context);
        ComponentHost componentHost = new(
            new ComponentRuntimeOptions(
                applicationContext.Components,
                new ApplicationWatchScheduler(),
                applicationContext.Services,
                applicationContext.ErrorHandler,
                applicationContext.WarnHandler,
                applicationContext.EventObserver));
        SsrRenderState state = new(
            writer,
            context,
            applicationContext,
            componentHost,
            cancellationToken);

        await ServerMarkupSerializer.RenderAsync(
            state,
            applicationContext.RootComponent,
            null).ConfigureAwait(false);
        ServerRenderStatePayload.CaptureAndAppend(state);
        await state.FlushAsync().ConfigureAwait(false);
        context.ResolveTeleports();
    }
}
