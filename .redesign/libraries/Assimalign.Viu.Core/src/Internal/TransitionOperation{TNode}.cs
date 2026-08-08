using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal enum TransitionOperationKind
{
    Enter,
    Leave,
}

internal sealed class TransitionOperation<TNode>
    where TNode : notnull
{
    internal TransitionOperation(
        TransitionOperationKind kind,
        TNode element,
        TransitionNode transition,
        Action<bool> completion)
    {
        Kind = kind;
        Element = element;
        Transition = transition;
        Completion = completion;
    }

    internal TransitionOperationKind Kind { get; }

    internal TNode Element { get; }

    internal TransitionNode Transition { get; }

    internal Action<bool> Completion { get; }

    internal bool IsCompleted { get; set; }
}
