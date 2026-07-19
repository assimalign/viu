using System;
using System.Diagnostics;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A ref whose value is projected through a getter (and optional setter) delegate — the backing
/// implementation of <see cref="Reactive.ToRef{T}(Func{T}, Action{T})"/> and the write-through refs a
/// generated <c>ToReferences()</c> hands out. It is the C# port of Vue 3.5's <c>toRef()</c> object
/// refs (<c>GetterRefImpl</c>/<c>ObjectRefImpl</c>,
/// https://vuejs.org/api/reactivity-utilities.html#toref): unlike <see cref="Reference{T}"/> it owns
/// no <see cref="Dependency"/> of its own, so tracking and triggering flow entirely through whatever
/// reactive source the delegates touch (e.g. a generated property's getter tracks and its setter
/// triggers). A ref created without a setter is read-only: a write logs a dev-mode warning and does
/// nothing, mirroring a getter-only <c>toRef</c>. Not thread-safe (single-threaded JS event-loop model).
/// </summary>
/// <typeparam name="T">The type of the projected value.</typeparam>
internal sealed class AccessorReference<T> : IReference<T>
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

    /// <summary>
    /// Gets the projected value (invoking the getter, which tracks whatever it reads) or routes a
    /// write through the setter. With no setter the write is a warned no-op (read-only <c>toRef</c>).
    /// </summary>
    public T Value
    {
        get => _getter();
        set
        {
            if (_setter is null)
            {
                Debug.WriteLine("[Vue warn] Write operation failed: this ref is readonly (it was created without a setter).");
                return;
            }
            _setter(value);
        }
    }

    /// <inheritdoc />
    object? IReference.Value => Value;
}
