namespace Assimalign.Vue.Reactivity;

/// <summary>
/// A reactive single-value container — the C# counterpart of Vue's <c>ref()</c> and the PRIMARY
/// reactivity primitive in Vuecs (C# has no JS <c>Proxy</c>, so the role of <c>reactive()</c> is
/// largely carried by refs). Unlike Vue's deep ref there is no deep conversion in C#:
/// <see cref="Ref{T}"/> tracks the <see cref="Value"/> cell only — mutations inside the held
/// object are not observed unless that object itself uses reactive primitives.
/// Change detection uses <see cref="EqualityComparer{T}.Default"/>: like <c>Object.is</c>, NaN is
/// self-equal; unlike <c>Object.is</c>, <c>+0.0</c> and <c>-0.0</c> compare equal (a deliberate
/// .NET divergence). Setting an equal value does not trigger.
/// Not thread-safe: designed for the single-threaded JS event-loop model.
/// </summary>
/// <typeparam name="T">The type of the contained value (never boxed for struct types).</typeparam>
public sealed class Ref<T> : IRef<T>, ITrackedRef
{
    private readonly Dep _dep = new();
    private T _value;

    /// <summary>Creates a ref holding <paramref name="value"/>.</summary>
    /// <param name="value">The initial value.</param>
    public Ref(T value)
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
