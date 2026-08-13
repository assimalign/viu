using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedLazyHydration<TNode> : MountedNode<TNode>
    where TNode : notnull
{
    internal MountedLazyHydration(
        ComponentNode value,
        HydrationNodeReader<TNode> reader,
        TNode container,
        TNode startAnchor,
        TNode endAnchor,
        RuntimeComponentContext? owner)
        : base(value, owner)
    {
        Reader = reader;
        Container = container;
        StartAnchor = startAnchor;
        EndAnchor = endAnchor;
    }

    internal HydrationNodeReader<TNode> Reader;

    internal TNode Container;

    internal TNode StartAnchor;

    internal TNode EndAnchor;

    internal MountedComponent<TNode>? ActivatedComponent;

    internal ComponentActivation? PendingActivation;

    internal int PendingComponentIdentifier;

    internal IHydrationTriggerRegistration? TriggerRegistration;

    internal SchedulerJob? ActivationJob;

    internal override TNode FirstHostNode => StartAnchor;

    internal override TNode LastHostNode => EndAnchor;
}
