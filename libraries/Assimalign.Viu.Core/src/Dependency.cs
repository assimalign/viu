using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// A single reactive dependency cell — the C# port of Vue 3.5's <c>Dep</c>. Maintains a version
/// counter and an intrusive doubly-linked list of subscriber <see cref="SubscriberLink"/>s.
/// <see cref="Track"/> links the ambient active subscriber (deduplicating via link versions);
/// <see cref="Trigger"/> bumps this dependency's version plus the global version and notifies
/// subscribers. Not thread-safe: designed for the single-threaded JS event-loop model.
/// </summary>
public sealed class Dependency
{
    /// <summary>Bumped on every trigger; compared against link versions to detect staleness.</summary>
    internal int Version;

    /// <summary>
    /// The link between this dependency and the current active subscriber, if any — lets a re-read
    /// within the same run reuse the existing link with zero allocation.
    /// </summary>
    internal SubscriberLink? ActiveLink;

    /// <summary>Tail of the subscriber list (notification iterates tail-to-head, Vue parity).</summary>
    internal SubscriberLink? Subscribers;

    /// <summary>Set when this dependency is owned by a computed (dual dependency/subscriber role).</summary>
    internal Subscriber? Computed;

    /// <summary>Total number of subscriber links (used for object-keyed dependency-map cleanup).</summary>
    internal int SubscriberCount;

    /// <summary>Owning key-to-dependency map, when this dependency lives in an object-keyed map.</summary>
    internal Dictionary<object, Dependency>? Map;

    /// <summary>The key under which this dependency is stored in <see cref="Map"/>.</summary>
    internal object? Key;

    /// <summary>
    /// Registers the ambient active subscriber as depending on this dependency. No-op when there is
    /// no active subscriber, tracking is paused, or the subscriber is this dependency's own
    /// computed.
    /// </summary>
    public void Track() => TrackLink();

    /// <summary>Core of <see cref="Track"/>; returns the (new or reused) link for computed reads.</summary>
    internal SubscriberLink? TrackLink()
    {
        var subscriber = ReactivityState.ActiveSubscriber;
        if (subscriber is null || !ReactivityState.ShouldTrack || ReferenceEquals(subscriber, Computed))
        {
            return null;
        }
        var link = ActiveLink;
        if (link is null || !ReferenceEquals(link.Subscriber, subscriber))
        {
            link = ActiveLink = new SubscriberLink(subscriber, this);

            // Append to the subscriber's dependency list tail.
            if (subscriber.Dependencies is null)
            {
                subscriber.Dependencies = subscriber.DependenciesTail = link;
            }
            else
            {
                link.PreviousDependency = subscriber.DependenciesTail;
                subscriber.DependenciesTail!.NextDependency = link;
                subscriber.DependenciesTail = link;
            }
            SubscriberOperations.AddSubscriber(link);
        }
        else if (link.Version == -1)
        {
            // Reused from a previous run: sync the version and move to the list tail so the
            // dependency list reflects this run's evaluation order.
            link.Version = Version;
            if (link.NextDependency is not null)
            {
                var next = link.NextDependency;
                next.PreviousDependency = link.PreviousDependency;
                if (link.PreviousDependency is not null)
                {
                    link.PreviousDependency.NextDependency = next;
                }
                link.PreviousDependency = subscriber.DependenciesTail;
                link.NextDependency = null;
                subscriber.DependenciesTail!.NextDependency = link;
                subscriber.DependenciesTail = link;
                if (ReferenceEquals(subscriber.Dependencies, link))
                {
                    subscriber.Dependencies = next;
                }
            }
        }
        return link;
    }

    /// <summary>
    /// Signals that the tracked value changed: bumps this dependency's version and the global
    /// version, then notifies all subscribers (batched — nested triggers coalesce).
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
            for (var link = Subscribers; link is not null; link = link.PreviousSubscriber)
            {
                if (link.Subscriber.Notify())
                {
                    // A computed became dirty: propagate to the computed's own readers.
                    link.Subscriber.NotifyReaders();
                }
            }
        }
        finally
        {
            ReactivityState.EndBatch();
        }
    }
}
