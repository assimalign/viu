using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedTree<TNode>
    where TNode : notnull
{
    internal IApplicationContext? Application;

    internal MountedRenderNode<TNode>? Root;

    internal Dictionary<IComponent, MountedRenderNode<TNode>> Components { get; } =
        new(ReferenceEqualityComparer.Instance);
}
