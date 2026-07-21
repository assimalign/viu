namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Non-generic marker for all ref-like reactive containers (<see cref="Reference{T}"/>,
/// <see cref="ShallowReference{T}"/>, <see cref="CustomReference{T}"/>, <see cref="Computed{T}"/>).
/// Enables <c>IsRef</c>/<c>Unref</c>-style introspection without reflection.
/// </summary>
public interface IReference
{
    /// <summary>The current value as <see cref="object"/> (boxes value types); reading tracks.</summary>
    object? Value { get; }
}
