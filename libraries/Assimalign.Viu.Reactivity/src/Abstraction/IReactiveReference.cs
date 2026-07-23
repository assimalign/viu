namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The non-generic contract for every ref-like reactive value. It is the C# counterpart of Vue
/// 3.5's untyped <c>Ref</c> shape
/// (https://vuejs.org/api/reactivity-core.html#ref) and enables reflection-free reference
/// inspection.
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
