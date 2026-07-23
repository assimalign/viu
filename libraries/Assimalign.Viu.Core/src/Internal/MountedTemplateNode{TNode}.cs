using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

internal sealed class MountedTemplateNode<TNode> : MountedRenderNode<TNode>
    where TNode : notnull
{
    internal MountedTemplateNode(
        ITemplateComponent component,
        MountedComponent instance,
        MountedRenderNode<TNode> subtree,
        ReactiveEffect renderEffect,
        SchedulerJob renderJob,
        SchedulerJob mountedJob,
        SchedulerJob updatedJob,
        TNode fallbackContainer,
        string? elementNamespace,
        ComponentContext? owner)
        : base(component, owner)
    {
        Instance = instance;
        Subtree = subtree;
        RenderEffect = renderEffect;
        RenderJob = renderJob;
        MountedJob = mountedJob;
        UpdatedJob = updatedJob;
        FallbackContainer = fallbackContainer;
        ElementNamespace = elementNamespace;
    }

    internal MountedComponent Instance;

    internal MountedRenderNode<TNode> Subtree;

    internal ReactiveEffect RenderEffect;

    internal SchedulerJob RenderJob;

    internal SchedulerJob MountedJob;

    internal SchedulerJob UpdatedJob;

    internal TNode FallbackContainer;

    internal string? ElementNamespace;

    internal TransitionHooks? Transition;

    internal MountedKeepAliveState<TNode>? KeepAliveState;

    internal MountedSuspenseState<TNode>? SuspenseState;

    internal ITemplateComponent? PendingNodeLifecycleComponent;

    internal ITemplateComponent? PreviousNodeLifecycleComponent;

    internal override TNode FirstHostNode => Subtree.FirstHostNode;

    internal override TNode LastHostNode => Subtree.LastHostNode;
}
