namespace Assimalign.Vue.Reactivity;

/// <summary>
/// A shallow reactive single-value container — the counterpart of Vue's <c>shallowRef()</c>.
/// Only replacement of <see cref="Value"/> itself triggers; in-place mutation of the held object
/// never notifies. Use <see cref="Reactive.TriggerRef"/> to force notification after an in-place
/// mutation. (In C# <see cref="Ref{T}"/> is also cell-shallow, but <see cref="ShallowRef{T}"/>
/// keeps the explicit upstream API distinction.)
/// Change detection uses <see cref="EqualityComparer{T}.Default"/>: like <c>Object.is</c>, NaN is
/// self-equal; unlike <c>Object.is</c>, <c>+0.0</c> and <c>-0.0</c> compare equal (a deliberate
/// .NET divergence). Setting an equal value does not trigger.
/// Not thread-safe: designed for the single-threaded JS event-loop model.
/// </summary>
/// <typeparam name="T">The type of the contained value (never boxed for struct types).</typeparam>
public sealed class ShallowRef<T> : IRef<T>, ITrackedRef
{
    private readonly Dep _dep = new();
    private T _value;

    /// <summary>Creates a shallow ref holding <paramref name="value"/>.</summary>
    /// <param name="value">The initial value.</param>
    public ShallowRef(T value)
    {
        _value = value;
    }

    /// <summary>
    /// Gets or sets the contained value. Reads track the ambient subscriber; writes trigger
    /// subscribers only when the new value differs per <see cref="EqualityComparer{T}.Default"/>.
    /// </summary>
    public T Value
    {
        get
        {
            _dep.Track();
            return _value;
        }
        set
        {
            if (!EqualityComparer<T>.Default.Equals(_value, value))
            {
                _value = value;
                _dep.Trigger();
            }
        }
    }

    /// <inheritdoc />
    object? IRef.Value => Value;

    Dep ITrackedRef.Dep => _dep;
}
