using System.Collections.Generic;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A reference cell that notifies only on assignment of a new instance, never on mutation of the
/// instance it holds — the escape hatch for large or externally owned objects whose interiors must
/// not be tracked.
/// Only replacement of <see cref="Value"/> itself triggers; in-place mutation of the held object
/// never notifies. Use <see cref="Reactive.TriggerReference"/> to force notification after an
/// in-place mutation. (In C# <see cref="Reference{T}"/> is also cell-shallow, but
/// <see cref="ShallowReference{T}"/> keeps the distinction explicit at the declaration site.)
/// Change detection uses <see cref="EqualityComparer{T}.Default"/>. Consequently, NaN values are
/// self-equal and <c>+0.0</c> and <c>-0.0</c> compare equal. Setting an equal value does not
/// trigger. These equality semantics are stable across Viu's reference and collection primitives.
/// Not thread-safe: designed for the single-threaded JS event-loop model. Specified by
/// <c>[RCT-3]</c> and <c>[RCT-7]</c>.
/// </summary>
/// <typeparam name="T">The type of the contained value (never boxed for struct types).</typeparam>
public sealed class ShallowReference<T> : ReactiveValue<T>
{
    private T _value;

    /// <summary>Creates a shallow reference holding <paramref name="value"/>.</summary>
    /// <param name="value">The initial value.</param>
    internal ShallowReference(T value)
    {
        _value = value;
    }

    /// <summary>
    /// Gets or sets the contained value. Reads track the ambient subscriber; writes trigger
    /// subscribers only when the new value differs per <see cref="EqualityComparer{T}.Default"/>.
    /// </summary>
    public override T Value
    {
        get
        {
            _dependency.Track();
            return _value;
        }
        set
        {
            if (!EqualityComparer<T>.Default.Equals(_value, value))
            {
                _value = value;
                _dependency.Trigger();
            }
        }
    }
}
