using System.Diagnostics;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A strongly-typed reactive value container. Reading <see cref="Value"/>
/// inside an active subscriber establishes a dependency on this value's <see cref="Dependency"/>;
/// writing a changed value notifies subscribers. Concrete leaves — <see cref="Reference{T}"/>,
/// <see cref="ShallowReference{T}"/>, <see cref="CustomReference{T}"/>, and <see cref="Computed{T}"/>
/// — are all sealed so the JIT can devirtualize the <see cref="Value"/> accessor on the hot path.
/// Not thread-safe: designed for the single-threaded JS event-loop model.
/// </summary>
/// <typeparam name="T">The type of the contained value (never boxed by <see cref="Value"/>).</typeparam>
[DebuggerDisplay("Value = {Peek(),nq}")]
public abstract class ReactiveValue<T> : ReactiveValue, IReactiveReference<T>
{
    private protected ReactiveValue()
    {
    }

    /// <summary>
    /// Gets or sets the contained value. Reads track the ambient subscriber; changed writes trigger
    /// subscribers. A read-only implementation (a getter-only <see cref="Computed{T}"/> or a
    /// setter-less projected reference) warns on write and leaves the value unchanged; it never
    /// throws. Note that <c>.Value</c> appears in read <em>and</em> write positions in generated
    /// code — with no proxy to auto-unwrap a reference, the compiler alone decides the access form,
    /// so changing this member's shape is a change to the expression-binding contract
    /// (<c>[RCT-8]</c>).
    /// </summary>
    public abstract T Value { get; set; }

    /// <summary>
    /// Gets the current fresh value without subscribing the ambient caller. A stale computed still
    /// refreshes and tracks its own sources; only the caller's dependency collection is paused.
    /// Tracking state is restored even when the value getter throws. Specified by <c>[RCT-5]</c>.
    /// </summary>
    /// <returns>The current value.</returns>
    public T Peek()
    {
        ReactivityState.PauseTracking();
        try
        {
            return Value;
        }
        finally
        {
            ReactivityState.ResetTracking();
        }
    }

    /// <summary>
    /// The typed <see cref="Value"/> as <see cref="object"/> (boxing value types); reading tracks.
    /// Sealed so every reactive value boxes through its typed <see cref="Value"/> accessor.
    /// </summary>
    public sealed override object? BoxedValue => Value;
}
