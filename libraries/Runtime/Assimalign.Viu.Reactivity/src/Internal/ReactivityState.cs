using System;
using System.Runtime.ExceptionServices;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Ambient reactivity state: the active subscriber, the should-track switch, the global version
/// counter, and the batch queues. Browser execution uses one shared single-event-loop state;
/// request-oriented hosts select an execution-flow-local state before running user code.
/// </summary>
internal static class ReactivityState
{
    /// <summary>The subscriber currently collecting dependencies, if any.</summary>
    internal static Subscriber? ActiveSubscriber
    {
        get => ReactivityExecutionIsolation.Current.ActiveSubscriber;
        set => ReactivityExecutionIsolation.Current.ActiveSubscriber = value;
    }

    /// <summary>Whether dependency tracking is currently enabled.</summary>
    internal static bool ShouldTrack
    {
        get => ReactivityExecutionIsolation.Current.ShouldTrack;
        set => ReactivityExecutionIsolation.Current.ShouldTrack = value;
    }

    /// <summary>
    /// Whether a read right now would establish a dependency (there is an active subscriber and
    /// tracking is enabled). Lets allocation-conscious sources — the reactive collections — skip
    /// creating a per-key <see cref="Dependency"/> for reads that occur outside any effect.
    /// </summary>
    internal static bool CanTrack => ActiveSubscriber is not null && ShouldTrack;

    /// <summary>
    /// Incremented on every reactive mutation anywhere; lets computeds skip dependency traversal
    /// entirely when nothing anywhere in the graph has changed.
    /// </summary>
    internal static int GlobalVersion
    {
        get => ReactivityExecutionIsolation.Current.GlobalVersion;
        set => ReactivityExecutionIsolation.Current.GlobalVersion = value;
    }

    /// <summary>Pushes the current tracking state and disables tracking.</summary>
    internal static void PauseTracking()
    {
        ReactivityExecutionState state = ReactivityExecutionIsolation.Current;
        state.TrackStack.Push(state.ShouldTrack);
        state.ShouldTrack = false;
    }

    /// <summary>Pops the previously pushed tracking state (re-enables tracking when the stack is empty).</summary>
    internal static void ResetTracking()
    {
        ReactivityExecutionState state = ReactivityExecutionIsolation.Current;
        state.ShouldTrack = state.TrackStack.Count == 0 || state.TrackStack.Pop();
    }

    /// <summary>Queues a notified subscriber for execution when the outermost batch ends.</summary>
    internal static void Batch(Subscriber subscriber, bool isComputed)
    {
        ReactivityExecutionState state = ReactivityExecutionIsolation.Current;
        subscriber.Flags |= SubscriberFlags.Notified;
        if (isComputed)
        {
            subscriber.NextBatched = state.BatchedComputed;
            state.BatchedComputed = subscriber;
            return;
        }
        subscriber.NextBatched = state.BatchedSubscriber;
        state.BatchedSubscriber = subscriber;
    }

    /// <summary>Increments the batch depth; triggers are queued until the matching <see cref="EndBatch"/>.</summary>
    internal static void StartBatch() => ReactivityExecutionIsolation.Current.BatchDepth++;

    /// <summary>
    /// Decrements the batch depth and, when it reaches zero, flushes queued subscribers. Computeds
    /// only have their notified flag cleared (they re-evaluate lazily); effects are triggered. The
    /// first exception thrown by an effect is rethrown after the queue drains, so one failing
    /// effect never strands the others mid-flush.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is no open batch to close.</exception>
    internal static void EndBatch()
    {
        ReactivityExecutionState state = ReactivityExecutionIsolation.Current;
        if (state.BatchDepth == 0)
        {
            throw new InvalidOperationException("EndBatch called without a matching StartBatch.");
        }
        if (--state.BatchDepth > 0)
        {
            return;
        }
        if (state.BatchedComputed is not null)
        {
            Subscriber? computed = state.BatchedComputed;
            state.BatchedComputed = null;
            while (computed is not null)
            {
                var next = computed.NextBatched;
                computed.NextBatched = null;
                computed.Flags &= ~SubscriberFlags.Notified;
                computed = next;
            }
        }
        ExceptionDispatchInfo? error = null;
        while (state.BatchedSubscriber is not null)
        {
            Subscriber? subscriber = state.BatchedSubscriber;
            state.BatchedSubscriber = null;
            while (subscriber is not null)
            {
                var next = subscriber.NextBatched;
                subscriber.NextBatched = null;
                subscriber.Flags &= ~SubscriberFlags.Notified;
                if ((subscriber.Flags & SubscriberFlags.Active) != 0)
                {
                    try
                    {
                        ((ReactiveEffect)subscriber).TriggerInvalidation();
                    }
                    catch (Exception exception)
                    {
                        error ??= ExceptionDispatchInfo.Capture(exception);
                    }
                }
                subscriber = next;
            }
        }
        error?.Throw();
    }
}

