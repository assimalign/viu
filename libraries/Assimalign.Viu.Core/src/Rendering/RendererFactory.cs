using System;

namespace Assimalign.Viu;

/// <summary>Creates host-neutral renderers over explicit host operations.</summary>
/// <remarks>Specified by <c>[RND-HOST-1]</c>.</remarks>
public static class RendererFactory
{
    /// <summary>Creates one renderer that owns mounted state for its host containers.</summary>
    /// <typeparam name="TNode">The opaque host-node type.</typeparam>
    /// <param name="options">The complete host operation set.</param>
    /// <returns>The renderer.</returns>
    public static Renderer<TNode> CreateRenderer<TNode>(RendererOptions<TNode> options)
        where TNode : notnull
    {
        ArgumentNullException.ThrowIfNull(options);
        return new Renderer<TNode>(options);
    }
}
