using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Lazily evaluated, version-cached derived state.
/// A computed plays a dual role in the dependency graph: it is a <see cref="Dependency"/> to its
/// readers (the cell it inherits from <see cref="ReactiveValue"/>) and, through its internal
/// <see cref="ComputedSubscriber"/>, a <see cref="Subscriber"/> to its sources. The getter never
/// runs at construction; the first <see cref="Value"/> read evaluates it, and later reads return the
/// cached value unless a dependency changed (with a global-version fast path that skips traversal
/// entirely when nothing reactive changed anywhere). Recomputing to an equal value (per
/// <see cref="EqualityComparer{T}.Default"/>) does not notify downstream subscribers.
/// Computeds are deliberately never owned by an <see cref="EffectScope"/>: scope ownership exists
/// to stop side effects, and a computed has none — a computed created inside a scope keeps serving
/// fresh values after that scope stops. Cleanup is driven by the subscriber count instead: when the
/// last subscriber unsubscribes the computed soft-detaches from its sources, and the next tracked
/// read re-attaches it. Specified by <c>[RCT-11]</c>.
/// Not thread-safe: designed for the single-threaded JS event-loop model.
/// </summary>
/// <typeparam name="T">The computed value type.</typeparam>
public sealed class Computed<T> : ReactiveValue<T>
{
    private readonly Func<T> _getter;
    private readonly Action<T>? _setter;
    private readonly ComputedSubscriber _subscriber;
    private T? _value;
    private int _globalVersion = -1;

    /// <summary>
    /// Creates a computed over <paramref name="getter"/>; pass a <paramref name="setter"/> for the
    /// writable variant. Unlike effects, computeds deliberately never register with the active
    /// <see cref="EffectScope"/> (<c>[RCT-11]</c>): cleanup is driven by the subscriber
    /// count instead — losing the last subscriber soft-detaches the computed from its sources.
    /// </summary>
    /// <param name="getter">The derivation function (not invoked until the first read).</param>
    /// <param name="setter">Optional setter making the computed writable.</param>
    /// <exception cref="ArgumentNullException"><paramref name="getter"/> is null.</exception>
    internal Computed(Func<T> getter, Action<T>? setter = null)
    {
        ArgumentNullException.ThrowIfNull(getter);
        _getter = getter;
        _setter = setter;
        _subscriber = new ComputedSubscriber(this);

        // The readers-facing dependency (inherited from ReactiveValue) knows its owning subscriber so
        // the computed does not self-track when its getter reads the cell it also writes.
        _dependency.Computed = _subscriber;
        _subscriber.Flags = SubscriberFlags.Dirty;
    }

    /// <summary>
    /// Whether this computed has a setter, making <see cref="Value"/> assignable.
    /// </summary>
    public bool IsWritable => _setter is not null;

    /// <summary>
    /// A getter-only computed is read-only; a writable computed is not. Surfaced through
    /// <see cref="Reactive.IsReadOnly"/>.
    /// </summary>
    public override bool IsReadOnly => _setter is null;

    /// <summary>
    /// Gets the (possibly recomputed) value, tracking the ambient subscriber; or routes assignment
    /// through the setter delegate. Reads always track and always serve a fresh value — a computed
    /// never enters a stopped state, by design (see the type remarks on automatic
    /// subscriber-count-driven detach). Writing a getter-only computed warns and is a no-op rather
    /// than throwing: a template binding that assigns a derived value is an authoring mistake to
    /// report, not a reason to tear down a render.
    /// </summary>
    public override T Value
    {
        get
        {
            var link = _dependency.TrackLink();
            _subscriber.Refresh();
            if (link is not null)
            {
                link.Version = _dependency.Version;
            }
            return _value!;
        }
        set
        {
            if (_setter is null)
            {
                RuntimeWarnings.Warn("Write operation failed: computed value is readonly.");
                return;
            }
            _setter(value);
        }
    }

    /// <summary>
    /// The subscriber half of a computed's dual dependency/subscriber role, realized by
    /// <b>composition</b> rather than inheritance (<c>[RCT-3]</c>): a computed must already be a
    /// <see cref="ReactiveValue{T}"/>, and C# has no second base class to spend. A nested type
    /// reaches the owner's private getter/value/version state without widening it.
    /// </summary>
    private sealed class ComputedSubscriber : Subscriber
    {
        private readonly Computed<T> _owner;

        internal ComputedSubscriber(Computed<T> owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// Re-evaluates the getter when a source has changed, and updates the cached value.
        /// </summary>
        internal override void Refresh()
        {
            // Fast bail: a tracked computed that no dependency has dirtied cannot be stale.
            if ((Flags & SubscriberFlags.Tracking) != 0 && (Flags & SubscriberFlags.Dirty) == 0)
            {
                return;
            }
            Flags &= ~SubscriberFlags.Dirty;

            // Global-version fast path: nothing reactive changed anywhere since the last evaluation.
            if (_owner._globalVersion == ReactivityState.GlobalVersion)
            {
                return;
            }
            _owner._globalVersion = ReactivityState.GlobalVersion;

            Flags |= SubscriberFlags.Running;
            var dependency = _owner.Dependency;
            if (dependency.Version > 0 && Dependencies is not null && (Flags & SubscriberFlags.Evaluated) != 0
                && !SubscriberOperations.IsDirty(this))
            {
                Flags &= ~SubscriberFlags.Running;
                return;
            }
            var previousSubscriber = ReactivityState.ActiveSubscriber;
            var previousShouldTrack = ReactivityState.ShouldTrack;
            ReactivityState.ActiveSubscriber = this;
            ReactivityState.ShouldTrack = true;
            try
            {
                SubscriberOperations.PrepareDependencies(this);
                var value = _owner._getter();
                Flags |= SubscriberFlags.Evaluated;

                // Equal-value cutoff: only bump the dependency version (notifying readers on their next
                // dirtiness check) when the value actually changed.
                if (dependency.Version == 0 || !EqualityComparer<T>.Default.Equals(value, _owner._value!))
                {
                    _owner._value = value;
                    dependency.Version++;
                }
            }
            catch
            {
                dependency.Version++;

                // Un-poison the bookkeeping synced above so the NEXT read re-invokes the getter
                // instead of fast-pathing to a stale value; the current reader gets the exception.
                _owner._globalVersion = -1;
                Flags |= SubscriberFlags.Dirty;
                Flags &= ~SubscriberFlags.Evaluated;
                throw;
            }
            finally
            {
                ReactivityState.ActiveSubscriber = previousSubscriber;
                ReactivityState.ShouldTrack = previousShouldTrack;
                SubscriberOperations.CleanupDependencies(this);
                Flags &= ~SubscriberFlags.Running;
            }
        }

        /// <summary>
        /// Propagates a real value change to the computed's own readers by notifying the
        /// <see cref="Dependency"/> they subscribe through (invoked by the caller only when
        /// <see cref="Notify"/> returned <see langword="true"/>).
        /// </summary>
        internal override void NotifyReaders() => _owner.Dependency.Notify();

        /// <summary>
        /// Called when a source dependency triggers: marks the computed dirty and, when it is being
        /// observed, queues it and returns <see langword="true"/> so the caller propagates to the
        /// computed's own readers.
        /// </summary>
        internal override bool Notify()
        {
            Flags |= SubscriberFlags.Dirty;
            if ((Flags & SubscriberFlags.Notified) == 0 && !ReferenceEquals(ReactivityState.ActiveSubscriber, this))
            {
                ReactivityState.Batch(this, isComputed: true);
                return true;
            }
            return false;
        }
    }
}
