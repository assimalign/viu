using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A ref with explicit control over dependency tracking and triggering — the counterpart of Vue's
/// <c>customRef()</c>. The factory receives <c>track</c>/<c>trigger</c> delegates bound to this
/// ref's dependency and returns the getter/setter; the ref performs no automatic tracking,
/// triggering, or change detection of its own. Not thread-safe (single-threaded JS event-loop
/// model).
/// </summary>
/// <typeparam name="T">The type of the contained value.</typeparam>
public sealed class CustomReference<T> : IReference<T>, ITrackedReference
{
    private readonly Dependency _dependency = new();
    private readonly Func<T> _get;
    private readonly Action<T> _set;

    /// <summary>Creates a custom ref from the given factory.</summary>
    /// <param name="factory">Receives track/trigger delegates and returns the getter/setter pair.</param>
    /// <exception cref="ArgumentNullException">The factory, or a member of the pair it returned, is null.</exception>
    public CustomReference(CustomReferenceFactory<T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var (get, set) = factory(_dependency.Track, _dependency.Trigger);
        _get = get ?? throw new ArgumentNullException(nameof(factory), "The factory returned a null getter.");
        _set = set ?? throw new ArgumentNullException(nameof(factory), "The factory returned a null setter.");
    }

    /// <summary>Gets or sets the value through the factory-provided getter/setter.</summary>
    public T Value
    {
        get => _get();
        set => _set(value);
    }

    /// <inheritdoc />
    object? IReference.Value => Value;

    Dependency ITrackedReference.Dependency => _dependency;
}
