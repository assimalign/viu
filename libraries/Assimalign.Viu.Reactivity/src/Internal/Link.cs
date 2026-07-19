namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The edge node connecting one <see cref="Dependency"/> to one <see cref="Subscriber"/>. Each
/// link participates in two intrusive doubly-linked lists — the subscriber's dependency list
/// (<see cref="PreviousDependency"/>/<see cref="NextDependency"/>) and the dependency's subscriber
/// list (<see cref="PreviousSubscriber"/>/<see cref="NextSubscriber"/>) — enabling O(1) unlink from
/// both sides. Links are reused across subscriber re-runs via <see cref="Version"/> (set to -1
/// before a run, refreshed on re-read; stale links are unlinked afterwards).
/// </summary>
internal sealed class Link
{
    /// <summary>The subscriber side of the edge.</summary>
    internal readonly Subscriber Subscriber;

    /// <summary>The dependency side of the edge.</summary>
    internal readonly Dependency Dependency;

    /// <summary>
    /// The dependency version observed when this link was last confirmed. <c>-1</c> marks a link
    /// that has not (yet) been re-read during the current subscriber run.
    /// </summary>
    internal int Version;

    /// <summary>Next link in the subscriber's dependency list.</summary>
    internal Link? NextDependency;

    /// <summary>Previous link in the subscriber's dependency list.</summary>
    internal Link? PreviousDependency;

    /// <summary>Next link in the dependency's subscriber list.</summary>
    internal Link? NextSubscriber;

    /// <summary>Previous link in the dependency's subscriber list.</summary>
    internal Link? PreviousSubscriber;

    /// <summary>Saved <see cref="Dependency.ActiveLink"/> so nested subscriber runs can restore it.</summary>
    internal Link? PreviousActiveLink;

    internal Link(Subscriber subscriber, Dependency dependency)
    {
        Subscriber = subscriber;
        Dependency = dependency;
        Version = dependency.Version;
    }
}
