namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Base class for everything that subscribes to <see cref="Dependency"/> cells — reactive effects
/// and computeds. The C# port of Vue 3.5's <c>Subscriber</c>. It holds the subscriber's dependency
/// link list, its flags word, and the batch-queue pointer as plain <b>fields</b> (not interface
/// properties), so the engine's hot paths — dependency notification, dirty-checking, and batch
/// flushing — read them with direct field access and dispatch <see cref="Notify"/> /
/// <see cref="Refresh"/> through the class vtable instead of an interface stub. Interface dispatch
/// is measurably costlier than a virtual call, especially on the mono-wasm / NativeAOT targets this
/// framework runs on, and these members are touched on every trigger.
/// <para>
/// This type is <see langword="public"/> because <see cref="ReactiveEffect"/> and
/// <see cref="Computed{T}"/> derive from it and because it is the entry point for inspecting the
/// dependency graph (<see cref="FirstDependency"/>). It cannot be instantiated or subclassed outside
/// this assembly (its constructor is <see langword="private protected"/>), and its mutable engine
/// state — the dependency-list tail, the flags word, and the batch-queue pointer — stays
/// <see langword="internal"/>, so external code can observe the graph without corrupting it. Not
/// thread-safe: designed for the single-threaded JS event-loop model.
/// </para>
/// </summary>
public abstract class Subscriber
{
    /// <summary>Head of the subscriber's dependency link list.</summary>
    internal SubscriberLink? Dependencies;

    /// <summary>Tail of the subscriber's dependency link list.</summary>
    internal SubscriberLink? DependenciesTail;

    /// <summary>State flags for the subscriber.</summary>
    internal SubscriberFlags Flags;

    /// <summary>Intrusive next pointer for the batch queue.</summary>
    internal Subscriber? NextBatched;

    /// <summary>
    /// The head of this subscriber's dependency link list — the first edge to a
    /// <see cref="Dependency"/> this subscriber read, or <see langword="null"/> when it tracks
    /// nothing. Walk <see cref="SubscriberLink.NextDependency"/> from here to enumerate every tracked
    /// dependency. The C# port of the <c>deps</c> head on Vue 3.5's <c>Subscriber</c>
    /// (<c>packages/reactivity/src/effect.ts</c>). Read-only: the list is spliced only by the engine.
    /// </summary>
    public SubscriberLink? FirstDependency => Dependencies;

    private protected Subscriber()
    {
    }

    /// <summary>
    /// Called when a tracked dependency triggers. Returns <see langword="true"/> when the subscriber
    /// is a computed that wants its own readers notified in turn (Vue 3.5 semantics), in which case
    /// the caller invokes <see cref="NotifyReaders"/>.
    /// </summary>
    internal abstract bool Notify();

    /// <summary>
    /// Re-evaluates the subscriber if (and only if) it may be out of date. No-op for effects;
    /// <see cref="Computed{T}"/> overrides this with the port of Vue 3.5's <c>refreshComputed</c>.
    /// </summary>
    internal virtual void Refresh()
    {
    }

    /// <summary>
    /// Propagates a change to this subscriber's own readers. No-op for effects;
    /// <see cref="Computed{T}"/> overrides this to notify the <see cref="Dependency"/> through which
    /// its readers subscribe.
    /// </summary>
    internal virtual void NotifyReaders()
    {
    }
}

