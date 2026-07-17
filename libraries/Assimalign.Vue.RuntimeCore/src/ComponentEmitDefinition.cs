using System;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// Metadata for one declared emitted event — an entry of upstream's normalized <c>emits</c>
/// option (<c>packages/runtime-core/src/componentEmits.ts</c>,
/// https://vuejs.org/guide/components/events.html). Declared events' handler props are
/// excluded from attribute fallthrough.
/// </summary>
public sealed class ComponentEmitDefinition
{
    /// <summary>Creates the metadata for <paramref name="name"/>.</summary>
    /// <param name="name">The event name as emitted (e.g. <c>"change"</c>, <c>"update:modelValue"</c>).</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public ComponentEmitDefinition(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>The event name as emitted.</summary>
    public string Name { get; }

    /// <summary>
    /// An optional payload validator; returning false produces a dev warning (upstream:
    /// emits-option validators).
    /// </summary>
    public Func<object?[], bool>? Validator { get; init; }
}
