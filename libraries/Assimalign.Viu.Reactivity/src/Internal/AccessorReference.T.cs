using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A reference whose value is projected through a getter (and optional setter) delegate — the
/// backing implementation of <see cref="Reactive.ToRef{T}(Func{T}, Action{T})"/> and the
/// write-through references a generated <c>ToReferences()</c> hands out. Tracking and triggering
/// flow entirely through whatever reactive source the delegates touch (a generated property's
/// getter tracks and its setter triggers), so the <see cref="ReactiveValue.Dependency"/> this type
/// inherits is never subscribed — it holds no state of its own to notify about. A reference created
/// without a setter is read-only (<see cref="IsReadOnly"/>): a write warns and does nothing. Not
/// thread-safe (single-threaded JS event-loop model).
/// </summary>
/// <typeparam name="T">The type of the projected value.</typeparam>
internal sealed class AccessorReference<T> : ReactiveValue<T>
{
    private readonly Func<T> _getter;
    private readonly Action<T>? _setter;

    /// <summary>Creates a ref projecting through <paramref name="getter"/> and <paramref name="setter"/>.</summary>
    /// <param name="getter">Invoked on every read; its reactive reads establish the ref's dependencies.</param>
    /// <param name="setter">Invoked on every write, or <see langword="null"/> for a read-only ref.</param>
    internal AccessorReference(Func<T> getter, Action<T>? setter)
    {
        _getter = getter;
        _setter = setter;
    }

    /// <inheritdoc />
    public override bool IsReadOnly => _setter is null;

    /// <summary>
    /// Gets the projected value (invoking the getter, which tracks whatever it reads) or routes a
    /// write through the setter. With no setter the write is a warned no-op (read-only <c>toRef</c>).
    /// </summary>
    public override T Value
    {
        get => _getter();
        set
        {
            if (_setter is null)
            {
                RuntimeWarnings.Warn("Write operation failed: this ref is readonly (it was created without a setter).");
                return;
            }
            _setter(value);
        }
    }
}

