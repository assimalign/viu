namespace Assimalign.Viu.State;

/// <summary>
/// Exposes the state registry associated with a runtime context without making Components depend
/// on State.
/// </summary>
/// <remarks>
/// Core's mounted component context implements this capability. State can therefore resolve a
/// definition from an <see cref="Assimalign.Viu.Components.IComponentContext"/> while the
/// Components package remains independent of State.
/// </remarks>
public interface IStateStoreContext
{
    /// <summary>Gets the application state registry, or null when state was not configured.</summary>
    IStateStoreRegistry? State { get; }
}
