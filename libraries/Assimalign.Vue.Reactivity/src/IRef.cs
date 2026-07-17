namespace Assimalign.Vue.Reactivity;

/// <summary>
/// Non-generic marker for all ref-like reactive containers (<see cref="Ref{T}"/>,
/// <see cref="ShallowRef{T}"/>, <see cref="CustomRef{T}"/>, <see cref="Computed{T}"/>).
/// Enables <c>IsRef</c>/<c>Unref</c>-style introspection without reflection.
/// </summary>
public interface IRef
{
    /// <summary>The current value as <see cref="object"/> (boxes value types); reading tracks.</summary>
    object? Value { get; }
}
