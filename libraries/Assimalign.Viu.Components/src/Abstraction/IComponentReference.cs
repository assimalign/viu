namespace Assimalign.Viu.Components;

/// <summary>
/// Receives the host element or exposed component surface associated with a mounted tree value.
/// </summary>
/// <remarks>
/// Core assigns a non-null value after mount and assigns null when the binding changes or the
/// value unmounts. The contract is host-neutral; Core provides adapters for reactive and callback
/// references.
/// </remarks>
public interface IComponentReference
{
    /// <summary>Assigns the current mounted value, or null during teardown.</summary>
    /// <param name="value">The mounted host element or component surface.</param>
    void Set(object? value);
}
