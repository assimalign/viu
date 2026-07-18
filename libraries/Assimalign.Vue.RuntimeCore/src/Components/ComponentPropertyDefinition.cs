using System;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// Precomputed metadata for one declared prop — the C# stand-in for an entry of upstream's
/// normalized <c>props</c> option (<c>packages/runtime-core/src/componentProps.ts</c>,
/// https://vuejs.org/guide/components/props.html). Produced by the source generator or written
/// explicitly; the runtime never discovers props reflectively.
/// </summary>
public sealed class ComponentPropertyDefinition
{
    private readonly string? _kebabName;

    /// <summary>Creates the metadata for <paramref name="name"/>.</summary>
    /// <param name="name">The camelCase prop name (e.g. <c>"modelValue"</c>).</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public ComponentPropertyDefinition(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        var kebab = StyleAndClassNormalization.Hyphenate(name);
        _kebabName = string.Equals(kebab, name, StringComparison.Ordinal) ? null : kebab;
    }

    /// <summary>The camelCase prop name.</summary>
    public string Name { get; }

    /// <summary>
    /// The kebab-case equivalent when it differs (vnode props may use either casing,
    /// upstream parity), or null when the name has no casing variant.
    /// </summary>
    public string? KebabName => _kebabName;

    /// <summary>The default value applied when the prop is absent.</summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// A factory for the default, for reference/collection defaults that must not be shared
    /// across instances (upstream: function defaults). Wins over <see cref="DefaultValue"/>.
    /// </summary>
    public Func<object?>? DefaultFactory { get; init; }

    /// <summary>Whether omitting the prop produces a dev warning (upstream: <c>required</c>).</summary>
    public bool Required { get; init; }

    /// <summary>
    /// An optional validator; returning false produces a dev warning naming the component and
    /// prop (upstream: <c>validator</c>).
    /// </summary>
    public Func<object?, bool>? Validator { get; init; }

    internal object? ResolveDefault()
        => DefaultFactory is not null ? DefaultFactory() : DefaultValue;
}
