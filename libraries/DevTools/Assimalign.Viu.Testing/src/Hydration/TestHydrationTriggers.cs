using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Provides deterministic host triggers for deferred hydration tests.</summary>
/// <remarks>
/// Trigger methods invoke Core's request callback; the test scheduler still controls when the
/// queued post-flush activation runs. This host is not thread-safe. Specified by
/// <c>[HYD-LAZY-3]</c> through <c>[HYD-LAZY-5]</c>.
/// </remarks>
public sealed class TestHydrationTriggers
{
    private readonly List<Registration> _pending = [];

    /// <summary>Gets the number of unfired and undisposed trigger registrations.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>Gets the number of activations completed by Core.</summary>
    public int CompletedCount { get; private set; }

    /// <summary>Gets the number of captured interaction events replayed after activation.</summary>
    public int ReplayedInteractionCount { get; private set; }

    /// <summary>Fires the first pending idle trigger.</summary>
    public void TriggerIdle() => TriggerFirst(HydrationStrategyKind.Idle, null);

    /// <summary>Fires the first pending visibility trigger.</summary>
    public void TriggerVisible() => TriggerFirst(HydrationStrategyKind.Visible, null);

    /// <summary>Fires the pending trigger for a matching media condition.</summary>
    /// <param name="mediaQuery">The exact declared media condition.</param>
    public void TriggerMediaQuery(string mediaQuery)
    {
        ArgumentException.ThrowIfNullOrEmpty(mediaQuery);
        TriggerFirst(HydrationStrategyKind.MediaQuery, mediaQuery);
    }

    /// <summary>Captures and fires the first pending trigger configured for an event.</summary>
    /// <param name="eventName">The exact declared event name.</param>
    public void TriggerInteraction(string eventName)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        TriggerFirst(HydrationStrategyKind.Interaction, eventName);
    }

    internal IHydrationTriggerRegistration Schedule(
        HydrationTriggerRequest<TestNode> request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Registration registration = new(this, request);
        _pending.Add(registration);
        return registration;
    }

    private void TriggerFirst(HydrationStrategyKind kind, string? condition)
    {
        for (int index = 0; index < _pending.Count; index++)
        {
            Registration registration = _pending[index];
            if (registration.Request.Strategy.Kind != kind
                || !MatchesCondition(registration.Request.Strategy, condition))
            {
                continue;
            }

            _pending.RemoveAt(index);
            registration.Fire(condition);
            return;
        }

        throw new InvalidOperationException(
            $"No pending {kind} hydration trigger matched the requested condition.");
    }

    private static bool MatchesCondition(
        HydrationStrategy strategy,
        string? condition) => strategy.Kind switch
    {
        HydrationStrategyKind.MediaQuery => string.Equals(
            strategy.MediaQuery,
            condition,
            StringComparison.Ordinal),
        HydrationStrategyKind.Interaction => Contains(
            strategy.InteractionEvents,
            condition),
        _ => condition is null,
    };

    private static bool Contains(IReadOnlyList<string> values, string? value)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class Registration : IHydrationTriggerRegistration
    {
        private TestHydrationTriggers? _owner;
        private bool _fired;
        private bool _isInteraction;

        internal Registration(
            TestHydrationTriggers owner,
            HydrationTriggerRequest<TestNode> request)
        {
            _owner = owner;
            Request = request;
        }

        internal HydrationTriggerRequest<TestNode> Request { get; }

        internal void Fire(string? condition)
        {
            if (_owner is null || _fired)
            {
                return;
            }

            _fired = true;
            _isInteraction = Request.Strategy.Kind == HydrationStrategyKind.Interaction
                && condition is not null;
            Request.Trigger();
        }

        public void Complete()
        {
            if (_owner is not { } owner || !_fired)
            {
                return;
            }

            owner.CompletedCount++;
            if (_isInteraction)
            {
                owner.ReplayedInteractionCount++;
            }

            _owner = null;
        }

        public void Dispose()
        {
            if (_owner is not { } owner)
            {
                return;
            }

            _ = owner._pending.Remove(this);
            _owner = null;
        }
    }
}
