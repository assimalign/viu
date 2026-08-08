using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal abstract class MountedNode<TNode>
    where TNode : notnull
{
    private protected MountedNode(VirtualNode value, RuntimeComponentContext? owner)
    {
        Value = value;
        Owner = owner;
    }

    internal VirtualNode Value;

    internal RuntimeComponentContext? Owner;

    internal bool IsUnmounted;

    internal abstract TNode FirstHostNode { get; }

    internal abstract TNode LastHostNode { get; }
}
