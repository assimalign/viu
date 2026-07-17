namespace Assimalign.Vue.Reactivity;

/// <summary>
/// A single reactive dependency cell — the C# port of Vue 3.5's <c>Dep</c>. Maintains a version
/// counter and an intrusive doubly-linked list of subscriber <see cref="Link"/>s.
/// <see cref="Track"/> links the ambient active subscriber (deduplicating via link versions);
/// <see cref="Trigger"/> bumps this dep's version plus the global version and notifies subscribers.
/// Not thread-safe: designed for the single-threaded JS event-loop model.
/// </summary>
public sealed class Dep
{
    /// <summary>Bumped on every trigger; compared against link versions to detect staleness.</summary>
    internal int Version;

    /// <summary>
    /// The link between this dep and the current active subscriber, if any — lets a re-read within
    /// the same run reuse the existing link with zero allocation.
    /// </summary>
    internal Link? ActiveLink;

    /// <summary>Tail of the subscriber list (notification iterates tail-to-head, Vue parity).</summary>
    internal Link? Subs;

    /// <summary>Set when this dep is owned by a computed (dual dep/subscriber role).</summary>
    internal IComputed? Computed;

    /// <summary>Total number of subscriber links (used for object-keyed dep-map cleanup).</summary>
    internal int SubCount;

    /// <summary>Owning key-to-dep map, when this dep lives in an object-keyed map.</summary>
    internal Dictionary<object, Dep>? Map;

    /// <summary>The key under which this dep is stored in <see cref="Map"/>.</summary>
    internal object? Key;

    /// <summary>
    /// Registers the ambient active subscriber as depending on this dep. No-op when there is no
    /// active subscriber, tracking is paused, or the subscriber is this dep's own computed.
    /// </summary>
    public void Track() => TrackLink();

    /// <summary>Core of <see cref="Track"/>; returns the (new or reused) link for computed reads.</summary>
    internal Link? TrackLink()
    {
        var sub = ReactivityState.ActiveSub;
        if (sub is null || !ReactivityState.ShouldTrack || ReferenceEquals(sub, Computed))
        {
            return null;
        }
        var link = ActiveLink;
        if (link is null || !ReferenceEquals(link.Sub, sub))
        {
            link = ActiveLink = new Link(sub, this);

            // Append to the subscriber's dependency list tail.
            if (sub.Deps is null)
            {
                sub.Deps = sub.DepsTail = link;
            }
            else
            {
                link.PrevDep = sub.DepsTail;
                sub.DepsTail!.NextDep = link;
                sub.DepsTail = link;
            }
            SubscriberOps.AddSub(link);
        }
        else if (link.Version == -1)
        {
            // Reused from a previous run: sync the version and move to the list tail so the
            // dependency list reflects this run's evaluation order.
            link.Version = Version;
            if (link.NextDep is not null)
            {
                var next = link.NextDep;
                next.PrevDep = link.PrevDep;
                if (link.PrevDep is not null)
                {
                    link.PrevDep.NextDep = next;
                }
                link.PrevDep = sub.DepsTail;
                link.NextDep = null;
                sub.DepsTail!.NextDep = link;
                sub.DepsTail = link;
                if (ReferenceEquals(sub.Deps, link))
                {
                    sub.Deps = next;
                }
            }
        }
        return link;
    }

    /// <summary>
    /// Signals that the tracked value changed: bumps this dep's version and the global version,
    /// then notifies all subscribers (batched — nested triggers coalesce).
    /// </summary>
    public void Trigger()
    {
        Version++;
        ReactivityState.GlobalVersion++;
        Notify();
    }

    /// <summary>Notifies subscribers without bumping versions (used by computed propagation).</summary>
    internal void Notify()
    {
        ReactivityState.StartBatch();
        try
        {
            for (var link = Subs; link is not null; link = link.PrevSub)
            {
                if (link.Sub.Notify())
                {
                    // A computed became dirty: propagate to the computed's own readers.
                    ((IComputed)link.Sub).Dep.Notify();
                }
            }
        }
        finally
        {
            ReactivityState.EndBatch();
        }
    }
}
