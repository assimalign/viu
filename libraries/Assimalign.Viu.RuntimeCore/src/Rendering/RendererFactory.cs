using System;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// Builds renderers over platform node-ops — the C# port of <c>createRenderer</c> from
/// <c>@vue/runtime-core</c> (https://vuejs.org/api/custom-renderer.html). The renderer core
/// never performs JS interop itself: the browser package supplies interop-backed options
/// ([V01.01.04.01]), tests supply the in-memory tree ([V01.01.11.01]).
/// </summary>
public static class RendererFactory
{
    /// <summary>Creates a renderer over <paramref name="options"/> (upstream: <c>createRenderer(options)</c>).</summary>
    /// <typeparam name="TNode">The platform node type; <c>default</c> means "no node".</typeparam>
    /// <param name="options">The complete platform node-ops.</param>
    /// <returns>The mount/patch/unmount pipeline.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static Renderer<TNode> CreateRenderer<TNode>(RendererOptions<TNode> options)
        where TNode : notnull
    {
        ArgumentNullException.ThrowIfNull(options);
        return new Renderer<TNode>(options);
    }
}
