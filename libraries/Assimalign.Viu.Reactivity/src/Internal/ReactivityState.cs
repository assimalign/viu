using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Ambient (static) reactivity state: the active subscriber, the should-track switch, the global
/// version counter, and the batch queues. This type is NOT thread-safe by design — the runtime
/// targets the single-threaded JS event-loop model (browser WASM), so plain static fields are used
/// deliberately and no synchronization is performed.
/// </summary>
internal static class ReactivityState
{
    /// <summary>The subscriber currently collecting dependencies, if any.</summary>
    internal static Subscriber? ActiveSubscriber;

    /// <summary>Whether dependency tracking is currently enabled.</summary>
    internal static bool ShouldTrack = true;

    /// <summary>
    /// Whether a read right now would establish a dependency (there is an active subscriber and
    /// tracking is enabled). Lets allocation-conscious sources — the reactive collections — skip
    /// creating a per-key <see cref="Dependency"/> for reads that occur outside any effect.
    /// </summary>
    internal static bool CanTrack => ActiveSubscriber is not null && ShouldTrack;

    /// <summary>
    /// Incremented on every reactive mutation anywhere; lets computeds skip dependency traversal
    /// entirely when nothing in the graph has changed (Vue 3.5 <c>globalVersion</c> fast path).
    /// </summary>
    internal static int GlobalVersion;

    private static readonly Stack<bool> TrackStack = new();
    private static int _batchDepth;
    private static Subscriber? _batchedSubscriber;
    private static Subscriber? _batchedComputed;

    /// <summary>Pushes the current tracking state and disables tracking.</summary>
    internal static void PauseTracking()
    {
        TrackStack.Push(ShouldTrack);
        ShouldTrack = false;
    }

    /// <summary>Pops the previously pushed tracking state (re-enables tracking when the stack is empty).</summary>
    internal static void ResetTracking()
    {
        ShouldTrack = TrackStack.Count == 0 || TrackStack.Pop();
    }

    /// <summary>Queues a notified subscriber for execution when the outermost batch ends.</summary>
    internal static void Batch(Subscriber subscriber, bool isComputed)
    {
        subscriber.Flags |= SubscriberFlags.Notified;
        if (isComputed)
        {
            subscriber.NextBatched = _batchedComputed;
            _batchedComputed = subscriber;
            return;
        }
        subscriber.NextBatched = _batchedSubscriber;
        _batchedSubscriber = subscriber;
    }

    /// <summary>Increments the batch depth; triggers are queued until the matching <see cref="EndBatch"/>.</summary>
    internal static void StartBatch() => _batchDepth++;

    /// <summary>
    /// Decrements the batch depth and, when it reaches zero, flushes queued subscribers. Computeds
    /// only have their notified flag cleared (they re-evaluate lazily); effects are triggered. The
    /// first exception thrown by an effect is rethrown after the queue drains (Vue parity).
    /// </summary>
    /// <exception cref="InvalidOperationException">There is no open batch to close.</exception>
    internal static void EndBatch()
    {
        if (_batchDepth == 0)
        {
            throw new InvalidOperationException("EndBatch called without a matching StartBatch.");
        }
        if (--_batchDepth > 0)
        {
            return;
        }
        if (_batchedComputed is not null)
        {
            var computed = _batchedComputed;
            _batchedComputed = null;
            while (computed is not null)
            {
                var next = computed.NextBatched;
                computed.NextBatched = null;
                computed.Flags &= ~SubscriberFlags.Notified;
                computed = next;
            }
        }
        ExceptionDispatchInfo? error = null;
        while (_batchedSubscriber is not null)
        {
            var subscriber = _batchedSubscriber;
            _batchedSubscriber = null;
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

