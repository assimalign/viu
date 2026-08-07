namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A reactive reference that exposes its dependency cell for forced triggering and dependency
/// graph inspection. Specified by <c>[RCT-4]</c> and <c>[RCT-9]</c>.
/// </summary>
public interface IReactiveTrackedReference : IReactiveReference
{
    /// <summary>Gets the dependency cell owned by the reference.</summary>
    Dependency Dependency { get; }
}
