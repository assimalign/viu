using System;

namespace Assimalign.Viu;

/// <summary>Creates host-neutral renderers over explicit platform operations.</summary>
public static class RendererFactory
{
    /// <summary>Creates a renderer over the supplied platform operations.</summary>
    /// <typeparam name="TNode">The platform node type.</typeparam>
    /// <param name="options">The complete platform-operation set.</param>
    /// <returns>The renderer.</returns>
    public static Renderer<TNode> CreateRenderer<TNode>(RendererOptions<TNode> options)
        where TNode : notnull
    {
        ArgumentNullException.ThrowIfNull(options);
        return new Renderer<TNode>(options);
    }
}
