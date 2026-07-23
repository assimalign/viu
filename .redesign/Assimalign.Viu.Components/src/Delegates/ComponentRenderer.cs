namespace Assimalign.Viu.Components;

/// <summary>Produces one template component's current rendered subtree.</summary>
/// <returns>The rendered subtree, or null for an empty placeholder.</returns>
public delegate IComponent? ComponentRenderer();

