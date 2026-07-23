namespace Assimalign.Viu.Components;

/// <summary>Creates one component template.</summary>
/// <returns>A fresh component template for one mount.</returns>
/// <remarks>
/// An activator may close over any application-owned resolver. The component package neither
/// supplies nor owns that resolver.
/// </remarks>
public delegate IComponentTemplate ComponentActivator();
