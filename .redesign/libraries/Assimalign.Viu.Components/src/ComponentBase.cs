namespace Assimalign.Viu.Components;

/// <summary>
/// The optional authoring base for hand-written and generated components. It stores the context
/// assigned during setup and deliberately does not implement <see cref="IComponent"/> — the
/// authored or generated partial opts into the contract explicitly.
/// </summary>
public abstract class ComponentBase
{
    /// <summary>Initializes the authoring base.</summary>
    protected ComponentBase()
    {
    }

    /// <summary>Gets or sets the mounted context assigned during setup.</summary>
    protected ComponentContext? Context { get; set; }
}
