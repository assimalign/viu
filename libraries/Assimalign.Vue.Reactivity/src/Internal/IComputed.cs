namespace Assimalign.Vue.Reactivity;

/// <summary>
/// Non-generic view of <see cref="Computed{T}"/> used by the engine: a computed is a
/// <see cref="ISubscriber"/> to its sources and owns a <see cref="Dependency"/> for its readers.
/// </summary>
internal interface IComputed : ISubscriber
{
    /// <summary>The dependency through which readers subscribe to this computed.</summary>
    Dependency Dependency { get; }

    /// <summary>
    /// Re-evaluates the computed if (and only if) it may be out of date — the C# port of Vue 3.5's
    /// <c>refreshComputed</c>, including the global-version fast path and equal-value cutoff.
    /// </summary>
    void Refresh();
}
