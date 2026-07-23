using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Renders the unified Viu component tree to HTML without a browser, DOM, or JavaScript interop host.
/// </summary>
/// <remarks>
/// Each template request is activated through the application-selected
/// <see cref="IComponentFactory"/>. Server-prefetch lifecycle callbacks are awaited before their
/// component subtree serializes. Render state is per call; applications should also be per request
/// when their borrowed services or state are request-scoped.
/// </remarks>
public static class ServerRenderer
{
    /// <summary>Renders a configured server application to an HTML string.</summary>
    /// <param name="application">The application to render.</param>
    /// <param name="context">The per-render context, or null for a new context.</param>
    /// <param name="cancellationToken">Cancels plugin initialization, prefetch, or writing.</param>
    /// <returns>The serialized HTML.</returns>
    public static async Task<string> RenderToStringAsync(
        ServerApplication application,
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
    /// Renders a primitive component tree without template activation or application services.
    /// </summary>
    /// <param name="rootComponent">The root tree value.</param>
    /// <param name="context">The per-render context, or null for a new context.</param>
    /// <param name="cancellationToken">Cancels rendering.</param>
    /// <returns>The serialized HTML.</returns>
    /// <remarks>
    /// If the tree contains a template request, render a <see cref="ServerApplication"/> configured
    /// with an <see cref="IComponentFactory"/> and service provider instead.
    /// </remarks>
    public static Task<string> RenderToStringAsync(
        IComponent rootComponent,
        SsrContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        return RenderToStringAsync(CreatePrimitiveApplication(rootComponent), context, cancellationToken);
    }

    /// <summary>Streams a configured server application to a text writer.</summary>
    /// <param name="application">The application to render.</param>
    /// <param name="writer">The destination writer.</param>
    /// <param name="context">The per-render context, or null for a new context.</param>
    /// <param name="cancellationToken">Cancels plugin initialization, prefetch, or writing.</param>
    /// <returns>A task that completes after all content is flushed.</returns>
    public static Task RenderToStreamAsync(
        ServerApplication application,
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
    /// Streams a primitive component tree without template activation or application services.
    /// </summary>
    /// <param name="rootComponent">The root tree value.</param>
    /// <param name="writer">The destination writer.</param>
    /// <param name="context">The per-render context, or null for a new context.</param>
    /// <param name="cancellationToken">Cancels rendering.</param>
    /// <returns>A task that completes after all content is flushed.</returns>
    public static Task RenderToStreamAsync(
        IComponent rootComponent,
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

    private static ServerApplication CreatePrimitiveApplication(IComponent rootComponent)
    {
        return new ServerApplication(
            rootComponent,
            EmptyComponentFactory.Instance,
            EmptyServiceProvider.Instance);
    }

    private static async Task RenderCoreAsync(
        ServerApplication application,
        SsrWriter writer,
        SsrContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await application.PrepareAsync(cancellationToken).ConfigureAwait(false);

        SsrRenderState state = new(
            writer,
            context,
            application.Context,
            cancellationToken);
        await ComponentTreeSerializer
            .RenderAsync(state, application.Context.RootComponent, owner: null)
            .ConfigureAwait(false);
        await state.FlushAsync().ConfigureAwait(false);
        context.ResolveTeleports();
    }
}
