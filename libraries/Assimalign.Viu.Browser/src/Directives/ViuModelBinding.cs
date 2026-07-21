using System;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The bound value a <c>v-model</c> directive carries — a snapshot of the current model value paired
/// with the setter that writes it back. This is the C# stand-in for how upstream threads the model
/// through a directive: Vue reads the value from <c>binding.value</c> and the setter from
/// <c>vnode.props['onUpdate:modelValue']</c> (<c>getModelAssigner</c>,
/// https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts). Viu has
/// no <c>this</c>-proxy and no reflection, so the compiler cannot stash a magic prop; instead the
/// <c>v-model</c> transform ([V01.01.05.03]) emits, per render,
/// <c>Directives.WithDirectives(vnode, VModelText.Instance, new ViuModelBinding(model, v =&gt; model = v))</c>
/// — the getter is the value already read into <see cref="Value"/>, the setter is the write-back
/// delegate. Both are plain delegates, never reflection over component members (AOT/trimming
/// contract).
/// <para>
/// The directive reads <see cref="Value"/> where upstream reads <c>binding.value</c>, calls
/// <see cref="Setter"/> where upstream calls <c>el[assignKey](v)</c>, and reads the previous
/// binding's <see cref="Value"/> where upstream reads <c>binding.oldValue</c>.
/// </para>
/// </summary>
public sealed class ViuModelBinding
{
    /// <summary>Creates a binding carrying the current model <paramref name="value"/> and its <paramref name="setter"/>.</summary>
    /// <param name="value">The current model value this render (upstream: <c>binding.value</c>).</param>
    /// <param name="setter">The write-back delegate invoked on element input (upstream: the model assigner).</param>
    /// <exception cref="ArgumentNullException"><paramref name="setter"/> is null.</exception>
    public ViuModelBinding(object? value, Action<object?> setter)
    {
        ArgumentNullException.ThrowIfNull(setter);
        Value = value;
        Setter = setter;
    }

    /// <summary>The current model value this render (upstream: <c>binding.value</c>).</summary>
    public object? Value { get; }

    /// <summary>The setter that writes a new value back to the model (upstream: the model assigner from <c>onUpdate:modelValue</c>).</summary>
    public Action<object?> Setter { get; }
}
