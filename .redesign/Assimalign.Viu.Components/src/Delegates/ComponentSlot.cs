namespace Assimalign.Viu.Components;

/// <summary>Produces content supplied to a named component slot.</summary>
/// <param name="arguments">The slot arguments supplied by the child.</param>
/// <returns>The slot subtree, or null for no content.</returns>
public delegate IComponent? ComponentSlot(IComponentArguments arguments);

