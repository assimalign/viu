namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Shared subscriber algorithms: link bookkeeping before/after a run, dirtiness checks, and
/// subscriber-list insertion/removal — the C# port of the helpers in Vue 3.5's <c>effect.ts</c>
/// (<c>prepareDeps</c>/<c>cleanupDeps</c>/<c>isDirty</c>/<c>addSub</c>/<c>removeSub</c>).
/// </summary>
internal static class SubscriberOperations
{
    /// <summary>
    /// Adds a link to its dependency's subscriber list. When the dependency belongs to a computed
    /// gaining its first subscriber, the computed lazily (re-)subscribes to its own sources.
    /// </summary>
    internal static void AddSubscriber(SubscriberLink link)
    {
        link.Dependency.SubscriberCount++;
        if ((link.Subscriber.Flags & SubscriberFlags.Tracking) != 0)
        {
            var computed = link.Dependency.Computed;
            if (computed is not null && link.Dependency.Subscribers is null)
            {
                // A computed gaining its first subscriber: flag it and re-subscribe its chain.
                computed.Flags |= SubscriberFlags.Tracking | SubscriberFlags.Dirty;
                for (var sourceLink = computed.Dependencies; sourceLink is not null; sourceLink = sourceLink.NextDependency)
                {
                    AddSubscriber(sourceLink);
                }
            }
            var currentTail = link.Dependency.Subscribers;
            if (!ReferenceEquals(currentTail, link))
            {
                link.PreviousSubscriber = currentTail;
                if (currentTail is not null)
                {
                    currentTail.NextSubscriber = link;
                }
            }
            link.Dependency.Subscribers = link;
        }
    }

    /// <summary>
    /// Removes a link from its dependency's subscriber list. When a computed loses its last
    /// subscriber it soft-unsubscribes from its own sources so unused computeds stop receiving
    /// notifications. A fully unsubscribed dependency removes itself from its owning object-keyed
    /// map.
    /// </summary>
    internal static void RemoveSubscriber(SubscriberLink link, bool soft = false)
    {
        var dependency = link.Dependency;
        var previousSubscriber = link.PreviousSubscriber;
        var nextSubscriber = link.NextSubscriber;
        if (previousSubscriber is not null)
        {
            previousSubscriber.NextSubscriber = nextSubscriber;
            link.PreviousSubscriber = null;
        }
        if (nextSubscriber is not null)
        {
            nextSubscriber.PreviousSubscriber = previousSubscriber;
            link.NextSubscriber = null;
        }
        if (ReferenceEquals(dependency.Subscribers, link))
        {
            dependency.Subscribers = previousSubscriber;
            if (previousSubscriber is null && dependency.Computed is not null)
            {
                // Last subscriber gone: the computed stops tracking and soft-unsubscribes
                // from its sources (links are kept for potential re-subscription).
                dependency.Computed.Flags &= ~SubscriberFlags.Tracking;
                for (var sourceLink = dependency.Computed.Dependencies; sourceLink is not null; sourceLink = sourceLink.NextDependency)
                {
                    RemoveSubscriber(sourceLink, soft: true);
                }
            }
        }
        if (!soft && --dependency.SubscriberCount == 0 && dependency.Map is not null && dependency.Key is not null)
        {
            dependency.Map.Remove(dependency.Key);
        }
    }

    /// <summary>Marks every link stale (version -1) and installs the links as their dependencies' active links.</summary>
    internal static void PrepareDependencies(Subscriber subscriber)
    {
        for (var link = subscriber.Dependencies; link is not null; link = link.NextDependency)
        {
            link.Version = -1;
            link.PreviousActiveLink = link.Dependency.ActiveLink;
            link.Dependency.ActiveLink = link;
        }
    }

    /// <summary>
    /// Unlinks every dependency not re-read during the latest run (version still -1) and restores
    /// each dependency's previous active link, then fixes up the subscriber's list head/tail.
    /// </summary>
    internal static void CleanupDependencies(Subscriber subscriber)
    {
        SubscriberLink? head = null;
        var tail = subscriber.DependenciesTail;
        var link = tail;
        while (link is not null)
        {
            var previous = link.PreviousDependency;
            if (link.Version == -1)
            {
                if (ReferenceEquals(link, tail))
                {
                    tail = previous;
                }
                RemoveSubscriber(link);
                RemoveDependency(link);
            }
            else
            {
                head = link;
            }
            link.Dependency.ActiveLink = link.PreviousActiveLink;
            link.PreviousActiveLink = null;
            link = previous;
        }
        subscriber.Dependencies = head;
        subscriber.DependenciesTail = tail;
    }

    /// <summary>
    /// Returns whether any tracked dependency changed since the link last observed it, refreshing
    /// computed sources along the way (their version bumps only on real value change).
    /// </summary>
    internal static bool IsDirty(Subscriber subscriber)
    {
        for (var link = subscriber.Dependencies; link is not null; link = link.NextDependency)
        {
            if (link.Dependency.Version != link.Version)
            {
                return true;
            }
            var computed = link.Dependency.Computed;
            if (computed is not null)
            {
                computed.Refresh();
                if (link.Dependency.Version != link.Version)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static void RemoveDependency(SubscriberLink link)
    {
        var previousDependency = link.PreviousDependency;
        var nextDependency = link.NextDependency;
        if (previousDependency is not null)
        {
            previousDependency.NextDependency = nextDependency;
            link.PreviousDependency = null;
        }
        if (nextDependency is not null)
        {
            nextDependency.PreviousDependency = previousDependency;
            link.NextDependency = null;
        }
    }
}

