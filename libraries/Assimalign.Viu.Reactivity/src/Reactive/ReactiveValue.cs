namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The abstract base of every ref-like reactive container in Viu — the plain, shallow, custom, and
/// projected refs (<see cref="Reference{T}"/>, <see cref="ShallowReference{T}"/>,
/// <see cref="CustomReference{T}"/>) and <see cref="Computed{T}"/>. It is the class-based C#
/// counterpart of the ref abstraction in <c>@vue/reactivity</c>
/// (<c>packages/reactivity/src/ref.ts</c>, https://vuejs.org/api/reactivity-core.html#ref): every
/// reactive value owns a single <see cref="Dependency"/> cell whose reads track the ambient
/// subscriber and whose writes notify it.
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
    /// This value's dependency cell — the port of the <c>dep</c> a ref carries in Vue 3.5
    /// (<c>packages/reactivity/src/ref.ts</c>). Allocated inline and never reassigned, so the public
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
    /// force-notifies through (the C# port of the <c>dep</c> accessor Vue 3.5 reads in
    /// <c>triggerRef</c>). The returned cell's own graph-mutating members are
    /// <see langword="internal"/>, so external code can observe the graph without corrupting it.
    /// </summary>
    public Dependency Dependency => _dependency;

    /// <summary>
    /// The current value as <see cref="object"/> (boxing value types); reading it establishes a
    /// dependency on the ambient subscriber. The port of reading <c>ref.value</c> without knowing
    /// its element type (https://vuejs.org/api/reactivity-core.html#ref) — used by the reflection-free
    /// introspection and deep-traversal paths that operate over an untyped <see cref="ReactiveValue"/>.
    /// </summary>
    public abstract object? BoxedValue { get; }

    object? IReactiveReference.Value => BoxedValue;

    /// <summary>
    /// Whether this value rejects writes (reads still track) — the C# port of Vue 3.5's
    /// <c>ReactiveFlags.IS_READONLY</c> flag consulted by <c>isReadonly()</c>
    /// (https://vuejs.org/api/reactivity-utilities.html#isreadonly), surfaced through
    /// <see cref="Reactive.IsReadonly"/>. A getter-only <see cref="Computed{T}"/> and a setter-less
    /// projected ref report <see langword="true"/>; a plain, shallow, custom, or writable computed
    /// ref reports <see langword="false"/>. Writing a read-only value warns and is a no-op (upstream
    /// parity); it never throws.
    /// </summary>
    public virtual bool IsReadOnly => false;
}
