namespace Assimalign.Vue.Reactivity;

/// <summary>
/// Contract shared by every node that subscribes to <see cref="Dependency"/> cells — reactive
/// effects and computeds. Mirrors Vue 3.5's <c>Subscriber</c> interface: a doubly-linked list of
/// <see cref="Link"/> nodes (head/tail), a flags word, and a notification entry point.
/// </summary>
internal interface ISubscriber
{
    /// <summary>Head of the subscriber's dependency link list.</summary>
    Link? Dependencies { get; set; }

    /// <summary>Tail of the subscriber's dependency link list.</summary>
    Link? DependenciesTail { get; set; }

    /// <summary>State flags for the subscriber.</summary>
    SubscriberFlags Flags { get; set; }

    /// <summary>Intrusive next pointer for the batch queue.</summary>
    ISubscriber? NextBatched { get; set; }

    /// <summary>
    /// Called when a tracked dependency triggers. Returns <see langword="true"/> when the
    /// subscriber is a computed that wants its own <see cref="Dependency"/> notified in turn
    /// (Vue 3.5 semantics).
    /// </summary>
    bool Notify();
}
