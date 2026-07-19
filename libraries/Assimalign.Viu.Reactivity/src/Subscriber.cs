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
/// This type is <see langword="public"/> only because <see cref="ReactiveEffect"/> and
/// <see cref="Computed{T}"/> derive from it; it exposes no public members and cannot be instantiated
/// or subclassed outside this assembly (its constructor is <see langword="private protected"/>). Not
/// thread-safe: designed for the single-threaded JS event-loop model.
/// </para>
/// </summary>
public abstract class Subscriber
{
    /// <summary>Head of the subscriber's dependency link list.</summary>
    internal Link? Dependencies;

    /// <summary>Tail of the subscriber's dependency link list.</summary>
    internal Link? DependenciesTail;

    /// <summary>State flags for the subscriber.</summary>
    internal SubscriberFlags Flags;

    /// <summary>Intrusive next pointer for the batch queue.</summary>
    internal Subscriber? NextBatched;

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
