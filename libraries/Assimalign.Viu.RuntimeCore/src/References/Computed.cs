using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Lazily evaluated, version-cached derived state — the C# port of Vue 3.5's <c>computed()</c>.
/// A computed plays a dual role in the dependency graph: it is a <see cref="Dependency"/> to its
/// readers and a subscriber to its sources. The getter never runs at construction; the first
/// <see cref="Value"/> read evaluates it, and later reads return the cached value unless a
/// dependency changed (with a global-version fast path that skips traversal entirely when nothing
/// reactive changed anywhere). Recomputing to an equal value (per
/// <see cref="EqualityComparer{T}.Default"/>) does not notify downstream subscribers.
/// Computeds are never owned by an <see cref="EffectScope"/> (upstream Vue 3.5 parity): a
/// computed created inside a scope stays fully reactive after the scope stops. Cleanup is
/// automatic instead — when the last subscriber unsubscribes, the computed soft-detaches from its
/// sources, and the next tracked read re-attaches it.
/// Not thread-safe: designed for the single-threaded JS event-loop model.
/// </summary>
/// <typeparam name="T">The computed value type.</typeparam>
public sealed class Computed<T> : Subscriber, IReference<T>, ITrackedReference, IReadonlyReactive
{
    private readonly Func<T> _getter;
    private readonly Action<T>? _setter;
    private readonly Dependency _dependency;
    private T? _value;
    private int _globalVersion = -1;

    /// <summary>
    /// Creates a computed over <paramref name="getter"/>; pass a <paramref name="setter"/> for the
    /// writable variant. Unlike effects, computeds never register with the active
    /// <see cref="EffectScope"/> (upstream Vue 3.5 parity): cleanup is driven by the subscriber
    /// count instead — losing the last subscriber soft-detaches the computed from its sources.
    /// </summary>
    /// <param name="getter">The derivation function (not invoked until the first read).</param>
    /// <param name="setter">Optional setter making the computed writable.</param>
    /// <exception cref="ArgumentNullException"><paramref name="getter"/> is null.</exception>
    public Computed(Func<T> getter, Action<T>? setter = null)
    {
        ArgumentNullException.ThrowIfNull(getter);
        _getter = getter;
        _setter = setter;
        _dependency = new Dependency { Computed = this };
        Flags = SubscriberFlags.Dirty;
    }

    /// <summary>Whether this computed has a setter (Vue's writable computed).</summary>
    public bool IsWritable => _setter is not null;

    /// <summary>
    /// A getter-only computed is read-only (the port of Vue's readonly <c>ComputedRef</c>); a
    /// writable computed is not. Surfaced through <see cref="Reactive.IsReadonly"/>.
    /// </summary>
    bool IReadonlyReactive.IsReadonly => _setter is null;

    /// <summary>
    /// Gets the (possibly recomputed) value, tracking the ambient subscriber; or routes assignment
    /// through the setter delegate. Reads always track and always serve a fresh value — a computed
    /// never enters a stopped state (upstream Vue 3.5 parity; see the type remarks on automatic
    /// subscriber-count-driven detach).
    /// </summary>
    /// <exception cref="NotSupportedException">Set was called on a computed without a setter.</exception>
    public T Value
    {
        get
        {
            var link = _dependency.TrackLink();
            Refresh();
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
                throw new NotSupportedException("Cannot write to a computed without a setter.");
            }
            _setter(value);
        }
    }

    /// <inheritdoc />
    object? IReference.Value => Value;

    /// <summary>Port of Vue 3.5's <c>refreshComputed</c>.</summary>
    internal override void Refresh()
    {
        // Fast bail: a tracked computed that no dependency has dirtied cannot be stale.
        if ((Flags & SubscriberFlags.Tracking) != 0 && (Flags & SubscriberFlags.Dirty) == 0)
        {
            return;
        }
        Flags &= ~SubscriberFlags.Dirty;

        // Global-version fast path: nothing reactive changed anywhere since the last evaluation.
        if (_globalVersion == ReactivityState.GlobalVersion)
        {
            return;
        }
        _globalVersion = ReactivityState.GlobalVersion;

        Flags |= SubscriberFlags.Running;
        var dependency = _dependency;
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
            var value = _getter();
            Flags |= SubscriberFlags.Evaluated;

            // Equal-value cutoff: only bump the dependency version (notifying readers on their next
            // dirtiness check) when the value actually changed.
            if (dependency.Version == 0 || !EqualityComparer<T>.Default.Equals(value, _value!))
            {
                _value = value;
                dependency.Version++;
            }
        }
        catch
        {
            dependency.Version++;

            // Un-poison the bookkeeping synced above so the NEXT read re-invokes the getter
            // instead of fast-pathing to a stale value; the current reader gets the exception.
            _globalVersion = -1;
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
    /// Propagates a real value change to this computed's own readers by notifying the
    /// <see cref="Dependency"/> they subscribe through (invoked by the caller only when
    /// <see cref="Notify"/> returned <see langword="true"/>).
    /// </summary>
    internal override void NotifyReaders() => _dependency.Notify();

    Dependency ITrackedReference.Dependency => _dependency;

    /// <summary>
    /// Called when a source dependency triggers: marks this computed dirty and, when it is being
    /// observed, queues it and returns <see langword="true"/> so the caller propagates to this
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
