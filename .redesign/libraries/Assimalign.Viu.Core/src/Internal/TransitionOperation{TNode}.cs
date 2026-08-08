using System;

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
        TransitionController controller,
        Action<bool> completion)
    {
        Kind = kind;
        Element = element;
        Controller = controller;
        Completion = completion;
    }

    internal TransitionOperationKind Kind { get; }

    internal TNode Element { get; }

    internal TransitionController Controller { get; }

    internal Action<bool> Completion { get; }

    internal bool IsCompleted { get; set; }
}
