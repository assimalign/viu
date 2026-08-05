using System;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The bound value a <c>v-model</c> directive carries — a snapshot of the current model value paired
/// with the setter that writes it back. Viu has no <c>this</c>-proxy and no reflection, so a
/// directive cannot recover the write-back path from a magically named property on the render node;
/// the value and its setter are passed explicitly instead. The
/// <c>v-model</c> transform ([V01.01.05.03], specified by <c>[SFC-CG-7]</c>) emits, per render,
/// <c>new object?[] { _vModelText, new ViuModelBinding(model, value =&gt; model = value) }</c> inside
/// the generated <c>withDirectives</c> tuple. Core resolves the directive marker through the
/// application directive resolver. The getter is the value already read into <see cref="Value"/>
/// and the setter is the write-back delegate. Both are plain values/delegates, never reflection
/// over component members (AOT/trimming contract).
/// <para>
/// A directive reads <see cref="Value"/> to reflect the model onto the element, calls
/// <see cref="Setter"/> to commit a user edit, and compares against the previous binding's
/// <see cref="Value"/> to decide whether anything changed.
/// </para>
/// </summary>
public sealed class ViuModelBinding
{
    /// <summary>Creates a binding carrying the current model <paramref name="value"/> and its <paramref name="setter"/>.</summary>
    /// <param name="value">The current model value this render.</param>
    /// <param name="setter">The write-back delegate invoked on element input.</param>
    /// <exception cref="ArgumentNullException"><paramref name="setter"/> is null.</exception>
    public ViuModelBinding(object? value, Action<object?> setter)
    {
        ArgumentNullException.ThrowIfNull(setter);
        Value = value;
        Setter = setter;
    }

    /// <summary>The current model value this render.</summary>
    public object? Value { get; }

    /// <summary>The setter that writes a new value back to the model.</summary>
    public Action<object?> Setter { get; }
}
