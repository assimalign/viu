using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Supplies a host with one marker-bounded deferred hydration trigger.</summary>
/// <typeparam name="TNode">The opaque host node type.</typeparam>
/// <remarks>
/// The request is a cold-path capability object. Core retains platform neutrality while Browser,
/// Testing, or another host interprets the strategy. Specified by <c>[HYD-LAZY-2]</c> and
/// <c>[HYD-LAZY-3]</c>.
/// </remarks>
public sealed class HydrationTriggerRequest<TNode>
    where TNode : notnull
{
    /// <summary>Initializes one host trigger request.</summary>
    /// <param name="strategy">The non-immediate strategy declared by the component invocation.</param>
    /// <param name="startAnchor">The adopted opening marker.</param>
    /// <param name="endAnchor">The adopted closing marker.</param>
    /// <param name="trigger">The callback the host invokes at most once.</param>
    public HydrationTriggerRequest(
        HydrationStrategy strategy,
        TNode startAnchor,
        TNode endAnchor,
        Action trigger)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(startAnchor);
        ArgumentNullException.ThrowIfNull(endAnchor);
        ArgumentNullException.ThrowIfNull(trigger);
        if (strategy.Kind == HydrationStrategyKind.Immediate)
        {
            throw new ArgumentException(
                "A host trigger request requires a deferred hydration strategy.",
                nameof(strategy));
        }

        Strategy = strategy;
        StartAnchor = startAnchor;
        EndAnchor = endAnchor;
        Trigger = trigger;
    }

    /// <summary>Gets the immutable trigger strategy.</summary>
    public HydrationStrategy Strategy { get; }

    /// <summary>Gets the adopted opening marker.</summary>
    public TNode StartAnchor { get; }

    /// <summary>Gets the adopted closing marker.</summary>
    public TNode EndAnchor { get; }

    /// <summary>
    /// Gets the at-most-once callback that the host invokes asynchronously after registration
    /// returns to schedule activation through Core.
    /// </summary>
    public Action Trigger { get; }
}
