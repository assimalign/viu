namespace Assimalign.Viu.Components;

/// <summary>
/// Supplies a component's compile-time registration through generic type constraints. Hosts can
/// describe components without constructing them or inspecting metadata. Specified by <c>[CMP-35]</c>.
/// </summary>
public interface IComponentDefinition
{
    /// <summary>Gets the immutable registration shared by every invocation of this definition.</summary>
    static abstract ComponentRegistration Registration { get; }
}
