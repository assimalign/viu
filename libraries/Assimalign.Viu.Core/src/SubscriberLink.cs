namespace Assimalign.Viu;

/// <summary>
/// The edge node connecting one <see cref="Dependency"/> to one <see cref="Subscriber"/> in the
/// reactive dependency graph — the C# port of Vue 3.5's <c>Link</c>
/// (<c>packages/reactivity/src/dep.ts</c>). Each link participates in two intrusive doubly-linked
/// lists — the subscriber's dependency list (<see cref="PreviousDependency"/>/<see cref="NextDependency"/>)
/// and the dependency's subscriber list (<see cref="PreviousSubscriber"/>/<see cref="NextSubscriber"/>)
/// — enabling O(1) unlink from both sides. Links are reused across subscriber re-runs via
/// <see cref="Version"/> (set to -1 before a run, refreshed on re-read; stale links are unlinked
/// afterwards).
/// <para>
/// This type is a read-only <b>window</b> onto the engine's dependency graph: .NET developers can
/// walk it to inspect what a subscriber depends on and which subscribers a dependency notifies, but
/// every state-mutating member is <see langword="internal"/> — links cannot be constructed,
/// re-pointed, or re-versioned from outside this assembly, so external code observes the graph
/// without being able to corrupt it. Not thread-safe: designed for the single-threaded JS
/// event-loop model.
/// </para>
/// </summary>
public sealed class SubscriberLink
{
    /// <summary>The subscriber side of the edge — the effect or computed that read the dependency.</summary>
    public Subscriber Subscriber { get; }

    /// <summary>The dependency side of the edge — the reactive cell that was read.</summary>
    public Dependency Dependency { get; }

    /// <summary>
    /// The dependency version observed when this link was last confirmed. <c>-1</c> marks a link
    /// that has not (yet) been re-read during the current subscriber run. Read-only from outside the
    /// engine: the setter is <see langword="internal"/> so external inspection cannot desynchronize
    /// the version bookkeeping.
    /// </summary>
    public int Version { get; internal set; }

    /// <summary>Next link in the subscriber's dependency list. Splicing is engine-internal.</summary>
    public SubscriberLink? NextDependency { get; internal set; }

    /// <summary>Previous link in the subscriber's dependency list. Splicing is engine-internal.</summary>
    public SubscriberLink? PreviousDependency { get; internal set; }

    /// <summary>Next link in the dependency's subscriber list. Splicing is engine-internal.</summary>
    public SubscriberLink? NextSubscriber { get; internal set; }

    /// <summary>Previous link in the dependency's subscriber list. Splicing is engine-internal.</summary>
    public SubscriberLink? PreviousSubscriber { get; internal set; }

    /// <summary>
    /// Saved <see cref="Dependency.ActiveLink"/> so nested subscriber runs can restore it. A
    /// transient engine save-slot, not part of the stable graph — kept fully internal.
    /// </summary>
    internal SubscriberLink? PreviousActiveLink;

    internal SubscriberLink(Subscriber subscriber, Dependency dependency)
    {
        Subscriber = subscriber;
        Dependency = dependency;
        Version = dependency.Version;
    }
}
