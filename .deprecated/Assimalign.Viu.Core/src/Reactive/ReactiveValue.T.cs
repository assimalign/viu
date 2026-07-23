namespace Assimalign.Viu;

/// <summary>
/// A strongly-typed reactive value container — the class-based C# counterpart of Vue 3.5's
/// <c>Ref&lt;T&gt;</c> (https://vuejs.org/api/reactivity-core.html#ref). Reading <see cref="Value"/>
/// inside an active subscriber establishes a dependency on this value's <see cref="Dependency"/>;
/// writing a changed value notifies subscribers. Concrete leaves — <see cref="Reference{T}"/>,
/// <see cref="ShallowReference{T}"/>, <see cref="CustomReference{T}"/>, and <see cref="Computed{T}"/>
/// — are all sealed so the JIT can devirtualize the <see cref="Value"/> accessor on the hot path.
/// Not thread-safe: designed for the single-threaded JS event-loop model.
/// </summary>
/// <typeparam name="T">The type of the contained value (never boxed by <see cref="Value"/>).</typeparam>
public abstract class ReactiveValue<T> : ReactiveValue
{
    private protected ReactiveValue()
    {
    }

    /// <summary>
    /// Gets or sets the contained value (Vue's <c>ref.value</c>). Reads track the ambient subscriber;
    /// changed writes trigger subscribers. A read-only implementation (a getter-only
    /// <see cref="Computed{T}"/> or a setter-less projected ref) warns on write and leaves the value
    /// unchanged — it never throws, matching Vue 3.5's readonly-write behavior
    /// (<c>packages/reactivity/src/computed.ts</c>).
    /// </summary>
    public abstract T Value { get; set; }

    /// <summary>
    /// The typed <see cref="Value"/> as <see cref="object"/> (boxing value types); reading tracks.
    /// Sealed so every reactive value boxes through its typed <see cref="Value"/> accessor.
    /// </summary>
    public sealed override object? BoxedValue => Value;
}
