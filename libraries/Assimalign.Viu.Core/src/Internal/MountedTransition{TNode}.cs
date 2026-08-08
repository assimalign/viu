using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedTransition<TNode> : MountedNode<TNode>
    where TNode : notnull
{
    internal MountedTransition(
        TransitionNode value,
        MountedNode<TNode> child,
        TransitionState sharedState,
        TransitionController controller,
        RuntimeComponentContext? owner)
        : base(value, owner)
    {
        Child = child;
        SharedState = sharedState;
        Controller = controller;
    }

    internal MountedNode<TNode> Child;

    internal TransitionState SharedState;

    internal TransitionController Controller;

    internal TransitionExecutionState State;

    internal bool IsUnmountPending;

    internal TransitionOperation<TNode>? EnterOperation;

    internal TransitionOperation<TNode>? LeaveOperation;

    internal MountedRange<TNode>? Overlap;

    internal MountedNode<TNode>? IncomingChild;

    internal override TNode FirstHostNode => Child.FirstHostNode;

    internal override TNode LastHostNode => Child.LastHostNode;
}
