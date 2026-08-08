namespace Assimalign.Viu;

internal sealed class TransitionMountContext<TNode>
    where TNode : notnull
{
    internal TransitionMountContext(
        TransitionController controller,
        TransitionMountContext<TNode>? parent,
        bool shouldEnter,
        bool isHydrating)
    {
        Controller = controller;
        Parent = parent;
        ShouldEnter = shouldEnter;
        IsHydrating = isHydrating;
    }

    internal TransitionController Controller { get; }

    internal TransitionMountContext<TNode>? Parent { get; }

    internal bool ShouldEnter { get; }

    internal bool IsHydrating { get; }

    internal bool IsSuppressed { get; private set; }

    internal bool IsClaimed { get; set; }

    internal bool BeforeEnterInvoked { get; set; }

    internal TNode? Element { get; set; }

    internal void Suppress() => IsSuppressed = true;
}
