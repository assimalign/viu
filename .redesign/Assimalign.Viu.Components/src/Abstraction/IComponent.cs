namespace Assimalign.Viu.Components;

/// <summary>
/// A value in Viu's render tree. This is the proposed common vocabulary for every value currently
/// represented by a virtual node.
/// </summary>
public interface IComponent
{
    /// <summary>Gets the component's renderer-dispatch kind.</summary>
    ComponentKind Kind { get; }

    /// <summary>Gets the optional identity used when diffing siblings.</summary>
    object? Key { get; }

    /// <summary>
    /// Gets compiler-produced optimization metadata for this tree value. Hand-authored values use
    /// <see cref="ComponentOptimization.None"/>.
    /// </summary>
    ComponentOptimization Optimization => ComponentOptimization.None;
}
