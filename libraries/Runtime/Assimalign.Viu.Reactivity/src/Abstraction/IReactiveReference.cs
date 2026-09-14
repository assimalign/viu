namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The non-generic contract for every reference-like reactive value: the untyped face of a
/// reference, so a caller can inspect and read one without knowing its element type. This is what
/// makes reference inspection reflection-free. Specified by <c>[RCT-1]</c>, <c>[RCT-2]</c>, and
/// <c>[RCT-4]</c>.
/// </summary>
/// <remarks>
/// First-party implementations should also derive from the internal engine's
/// <c>ReactiveValue</c> base so the public interface does not replace class dispatch on hot paths.
/// </remarks>
public interface IReactiveReference
{
    /// <summary>Gets the current value as an object. Reading the value establishes a dependency.</summary>
    object? Value { get; }

    /// <summary>
    /// Gets whether this reference represents computed state, including a writable computed.
    /// Inspection reads this classification without evaluating the value or tracking a dependency,
    /// so it can reject edits through computed ancestors. Implementations default to false.
    /// Specified by <c>[DVT-13]</c>.
    /// </summary>
    bool IsComputed => false;
}
