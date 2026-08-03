namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The non-generic contract for every reference-like reactive value: the untyped face of a
/// reference, so a caller can inspect and read one without knowing its element type. This is what
/// makes reference inspection reflection-free.
/// </summary>
/// <remarks>
/// First-party implementations should also derive from the internal engine's
/// <c>ReactiveValue</c> base so the public interface does not replace class dispatch on hot paths.
/// </remarks>
public interface IReactiveReference
{
    /// <summary>Gets the current value as an object. Reading the value establishes a dependency.</summary>
    object? Value { get; }
}
