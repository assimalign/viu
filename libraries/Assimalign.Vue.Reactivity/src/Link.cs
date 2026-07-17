namespace Assimalign.Vue.Reactivity;

/// <summary>
/// The edge node connecting one <see cref="Dep"/> to one <see cref="ISubscriber"/>. Each link
/// participates in two intrusive doubly-linked lists — the subscriber's dependency list
/// (<see cref="PrevDep"/>/<see cref="NextDep"/>) and the dep's subscriber list
/// (<see cref="PrevSub"/>/<see cref="NextSub"/>) — enabling O(1) unlink from both sides.
/// Links are reused across subscriber re-runs via <see cref="Version"/> (set to -1 before a run,
/// refreshed on re-read; stale links are unlinked afterwards).
/// </summary>
internal sealed class Link
{
    /// <summary>The subscriber side of the edge.</summary>
    internal readonly ISubscriber Sub;

    /// <summary>The dependency side of the edge.</summary>
    internal readonly Dep Dep;

    /// <summary>
    /// The dep version observed when this link was last confirmed. <c>-1</c> marks a link that has
    /// not (yet) been re-read during the current subscriber run.
    /// </summary>
    internal int Version;

    /// <summary>Next link in the subscriber's dependency list.</summary>
    internal Link? NextDep;

    /// <summary>Previous link in the subscriber's dependency list.</summary>
    internal Link? PrevDep;

    /// <summary>Next link in the dep's subscriber list.</summary>
    internal Link? NextSub;

    /// <summary>Previous link in the dep's subscriber list.</summary>
    internal Link? PrevSub;

    /// <summary>Saved <see cref="Dep.ActiveLink"/> so nested subscriber runs can restore it.</summary>
    internal Link? PrevActiveLink;

    internal Link(ISubscriber sub, Dep dep)
    {
        Sub = sub;
        Dep = dep;
        Version = dep.Version;
    }
}
