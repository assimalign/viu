using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// The server-rendering entry points — the C# port of <c>renderToString</c> and the streaming APIs of
/// <c>@vue/server-renderer</c> (https://vuejs.org/api/ssr.html,
/// <c>packages/server-renderer/src/renderToString.ts</c>). It walks a <see cref="ServerApplication"/>'s
/// component tree to HTML on a plain .NET host — no DOM, no JS interop — awaiting each component's
/// async <see cref="Lifecycle.OnServerPrefetch"/> before serializing its subtree, and surfacing
/// out-of-tree teleport content through the <see cref="SsrContext"/>.
/// <para>
/// Every call renders one <see cref="ServerApplication"/> instance and holds no static render state, so
/// concurrent requests with distinct app instances share nothing — the per-request app-instance
/// discipline Vue's SSR requires (any cross-request singleton holding reactive state is a correctness
/// bug). The single-threaded runtime model still applies within a render: a render must not interleave
/// with another on the same thread.
/// </para>
/// </summary>
public static class ServerRenderer
{
    /// <summary>
    /// Renders <paramref name="application"/> to an HTML string (upstream: <c>renderToString(app, context)</c>).
    /// </summary>
    /// <param name="application">The server application to render.</param>
    /// <param name="context">The render context (teleports, state handoff); a fresh one is created when null.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <returns>The serialized HTML.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="application"/> is null.</exception>
    public static async Task<string> RenderToStringAsync(
        ServerApplication application,
        SsrContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        var writer = new SsrWriter();
        await RenderCoreAsync(application, writer, context ?? new SsrContext(), cancellationToken).ConfigureAwait(false);
        return writer.ToStringResult();
    }

    /// <summary>
    /// Renders a root component to an HTML string (convenience over
    /// <see cref="RenderToStringAsync(ServerApplication, SsrContext?, CancellationToken)"/>).
    /// </summary>
    /// <param name="rootComponent">The root component definition.</param>
    /// <param name="rootProperties">The root component's props, or null.</param>
    /// <param name="context">The render context, or null for a fresh one.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <returns>The serialized HTML.</returns>
    public static Task<string> RenderToStringAsync(
        IComponent rootComponent,
        VirtualNodeProperties? rootProperties = null,
        SsrContext? context = null,
        CancellationToken cancellationToken = default)
        => RenderToStringAsync(new ServerApplication(rootComponent, rootProperties), context, cancellationToken);

    /// <summary>
    /// Renders <paramref name="application"/> to <paramref name="writer"/>, streaming each completed
    /// component subtree and honoring the writer's <see cref="TextWriter.FlushAsync()"/> backpressure
    /// (upstream: the streaming render APIs). The whole document is never buffered; teleport content is
    /// collected into <paramref name="context"/> for the host to place after the stream completes.
    /// </summary>
    /// <param name="application">The server application to render.</param>
    /// <param name="writer">The destination writer.</param>
    /// <param name="context">The render context (teleports, state handoff); a fresh one is created when null.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <exception cref="ArgumentNullException"><paramref name="application"/> or <paramref name="writer"/> is null.</exception>
    public static Task RenderToStreamAsync(
        ServerApplication application,
        TextWriter writer,
        SsrContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(writer);
        return RenderCoreAsync(application, new SsrWriter(writer), context ?? new SsrContext(), cancellationToken);
    }

    /// <summary>
    /// Renders a root component to <paramref name="writer"/> (convenience over
    /// <see cref="RenderToStreamAsync(ServerApplication, TextWriter, SsrContext?, CancellationToken)"/>).
    /// </summary>
    /// <param name="rootComponent">The root component definition.</param>
    /// <param name="writer">The destination writer.</param>
    /// <param name="rootProperties">The root component's props, or null.</param>
    /// <param name="context">The render context, or null for a fresh one.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    public static Task RenderToStreamAsync(
        IComponent rootComponent,
        TextWriter writer,
        VirtualNodeProperties? rootProperties = null,
        SsrContext? context = null,
        CancellationToken cancellationToken = default)
        => RenderToStreamAsync(new ServerApplication(rootComponent, rootProperties), writer, context, cancellationToken);

    private static async Task RenderCoreAsync(
        ServerApplication application,
        SsrWriter writer,
        SsrContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = new SsrRenderState(writer, context, cancellationToken);
        // Build the root vnode and attach the app context so every instance down the tree inherits the
        // registries and app-level provides (upstream: vnode.appContext set in app.mount / renderToString).
        var rootVirtualNode = VirtualNodeFactory.Component(application.RootComponent, application.RootProperties);
        rootVirtualNode.AppContext = application.Context;
        await VirtualNodeSerializer.RenderVirtualNodeAsync(state, rootVirtualNode, null).ConfigureAwait(false);
        await state.FlushAsync().ConfigureAwait(false);
        // Freeze teleport buffers into context.Teleports once the whole tree has serialized.
        context.ResolveTeleports();
    }
}
