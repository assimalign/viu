using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal abstract class MountedRenderNode<TNode>
    where TNode : notnull
{
    private protected MountedRenderNode(
        IComponent component,
        ComponentContext? owner)
    {
        Component = component;
        Owner = owner;
    }

    internal IComponent Component;

    internal ComponentContext? Owner;

    internal SchedulerJob? ReferenceJob;

    internal bool IsUnmounted;

    internal abstract TNode FirstHostNode { get; }

    internal abstract TNode LastHostNode { get; }
}
