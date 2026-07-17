namespace Assimalign.Vue.Reactivity;

/// <summary>
/// Shared subscriber algorithms: link bookkeeping before/after a run, dirtiness checks, and
/// subscriber-list insertion/removal — the C# port of the helpers in Vue 3.5's <c>effect.ts</c>
/// (<c>prepareDeps</c>/<c>cleanupDeps</c>/<c>isDirty</c>/<c>addSub</c>/<c>removeSub</c>).
/// </summary>
internal static class SubscriberOps
{
    /// <summary>
    /// Adds a link to its dep's subscriber list. When the dep belongs to a computed gaining its
    /// first subscriber, the computed lazily (re-)subscribes to its own sources.
    /// </summary>
    internal static void AddSub(Link link)
    {
        link.Dep.SubCount++;
        if ((link.Sub.Flags & SubscriberFlags.Tracking) != 0)
        {
            var computed = link.Dep.Computed;
            if (computed is not null && link.Dep.Subs is null)
            {
                // A computed gaining its first subscriber: flag it and re-subscribe its chain.
                computed.Flags |= SubscriberFlags.Tracking | SubscriberFlags.Dirty;
                for (var l = computed.Deps; l is not null; l = l.NextDep)
                {
                    AddSub(l);
                }
            }
            var currentTail = link.Dep.Subs;
            if (!ReferenceEquals(currentTail, link))
            {
                link.PrevSub = currentTail;
                if (currentTail is not null)
                {
                    currentTail.NextSub = link;
                }
            }
            link.Dep.Subs = link;
        }
    }

    /// <summary>
    /// Removes a link from its dep's subscriber list. When a computed loses its last subscriber it
    /// soft-unsubscribes from its own sources so unused computeds stop receiving notifications.
    /// A fully unsubscribed dep removes itself from its owning object-keyed map.
    /// </summary>
    internal static void RemoveSub(Link link, bool soft = false)
    {
        var dep = link.Dep;
        var prevSub = link.PrevSub;
        var nextSub = link.NextSub;
        if (prevSub is not null)
        {
            prevSub.NextSub = nextSub;
            link.PrevSub = null;
        }
        if (nextSub is not null)
        {
            nextSub.PrevSub = prevSub;
            link.NextSub = null;
        }
        if (ReferenceEquals(dep.Subs, link))
        {
            dep.Subs = prevSub;
            if (prevSub is null && dep.Computed is not null)
            {
                // Last subscriber gone: the computed stops tracking and soft-unsubscribes
                // from its sources (links are kept for potential re-subscription).
                dep.Computed.Flags &= ~SubscriberFlags.Tracking;
                for (var l = dep.Computed.Deps; l is not null; l = l.NextDep)
                {
                    RemoveSub(l, soft: true);
                }
            }
        }
        if (!soft && --dep.SubCount == 0 && dep.Map is not null && dep.Key is not null)
        {
            dep.Map.Remove(dep.Key);
        }
    }

    /// <summary>Marks every link stale (version -1) and installs the links as their deps' active links.</summary>
    internal static void PrepareDeps(ISubscriber sub)
    {
        for (var link = sub.Deps; link is not null; link = link.NextDep)
        {
            link.Version = -1;
            link.PrevActiveLink = link.Dep.ActiveLink;
            link.Dep.ActiveLink = link;
        }
    }

    /// <summary>
    /// Unlinks every dep not re-read during the latest run (version still -1) and restores each
    /// dep's previous active link, then fixes up the subscriber's list head/tail.
    /// </summary>
    internal static void CleanupDeps(ISubscriber sub)
    {
        Link? head = null;
        var tail = sub.DepsTail;
        var link = tail;
        while (link is not null)
        {
            var prev = link.PrevDep;
            if (link.Version == -1)
            {
                if (ReferenceEquals(link, tail))
                {
                    tail = prev;
                }
                RemoveSub(link);
                RemoveDep(link);
            }
            else
            {
                head = link;
            }
            link.Dep.ActiveLink = link.PrevActiveLink;
            link.PrevActiveLink = null;
            link = prev;
        }
        sub.Deps = head;
        sub.DepsTail = tail;
    }

    /// <summary>
    /// Returns whether any tracked dep changed since the link last observed it, refreshing
    /// computed sources along the way (their version bumps only on real value change).
    /// </summary>
    internal static bool IsDirty(ISubscriber sub)
    {
        for (var link = sub.Deps; link is not null; link = link.NextDep)
        {
            if (link.Dep.Version != link.Version)
            {
                return true;
            }
            var computed = link.Dep.Computed;
            if (computed is not null)
            {
                computed.Refresh();
                if (link.Dep.Version != link.Version)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static void RemoveDep(Link link)
    {
        var prevDep = link.PrevDep;
        var nextDep = link.NextDep;
        if (prevDep is not null)
        {
            prevDep.NextDep = nextDep;
            link.PrevDep = null;
        }
        if (nextDep is not null)
        {
            nextDep.PrevDep = prevDep;
            link.NextDep = null;
        }
    }
}
