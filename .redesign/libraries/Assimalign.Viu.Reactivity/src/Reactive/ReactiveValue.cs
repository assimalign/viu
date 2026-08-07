namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The abstract base of every reference-like reactive container in Viu — the plain, shallow,
/// custom, and projected references (<see cref="Reference{T}"/>, <see cref="ShallowReference{T}"/>,
/// <see cref="CustomReference{T}"/>) and <see cref="Computed{T}"/>. Every
/// reactive value owns a single <see cref="Dependency"/> cell whose reads track the ambient
/// subscriber and whose writes notify it. Specified by <c>[RCT-1]</c>.
/// <para>
/// A base <b>class</b> remains deliberate even though public introspection accepts
/// <see cref="IReactiveReference"/>: the shared dependency cell lives on the base as a field (a
/// direct load on the hot path), rather than behind interface dispatch inside dependency
/// notification. The base cannot be constructed or subclassed outside this assembly (its
/// constructor is <see langword="private protected"/>), so the first-party reactive value set
/// stays closed. Not thread-safe: designed for the single-threaded JS event-loop model.
/// </para>
/// </summary>
public abstract class ReactiveValue : IReactiveTrackedReference, IReactiveReadOnly
{
    /// <summary>
    /// This value's dependency cell, held inline as a field so the notification path is a direct
    /// load rather than a property call. Allocated inline and never reassigned, so the public
    /// <see cref="Dependency"/> property is a stable inspection handle that cannot be swapped out.
    /// </summary>
    private protected readonly Dependency _dependency = new();

    private protected ReactiveValue()
    {
    }

    /// <summary>
    /// The dependency that tracks reads of, and is triggered by writes to, this reactive value.
    /// Reading this property never tracks — it is the stable, reflection-free entry point into the
    /// dependency graph rooted at the value, and the handle <see cref="Reactive.TriggerReference"/>
    /// force-notifies through. The returned cell's own graph-mutating members are
    /// <see langword="internal"/>, so external code can observe the graph without corrupting it.
    /// </summary>
    public Dependency Dependency => _dependency;

    /// <summary>
    /// The current value as <see cref="object"/> (boxing value types); reading it establishes a
    /// dependency on the ambient subscriber. This is how a caller reads a reference without knowing
    /// its element type — used by the reflection-free introspection and deep-traversal paths that
    /// operate over an untyped <see cref="ReactiveValue"/>.
    /// </summary>
    public abstract object? BoxedValue { get; }

    object? IReactiveReference.Value => BoxedValue;

    /// <summary>
    /// Whether this value rejects writes (reads still track), surfaced through
    /// <see cref="Reactive.IsReadonly"/>. A getter-only <see cref="Computed{T}"/> and a setter-less
    /// projected reference report <see langword="true"/>; a plain, shallow, custom, or writable
    /// computed reference reports <see langword="false"/>. Writing a read-only value warns and is a
    /// no-op rather than throwing: a binding that assigns a derived value is an authoring mistake to
    /// report, not a reason to tear down a render.
    /// </summary>
    public virtual bool IsReadOnly => false;
}

