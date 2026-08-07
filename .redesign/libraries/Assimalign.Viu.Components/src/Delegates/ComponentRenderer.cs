namespace Assimalign.Viu.Components;

/// <summary>
/// Produces a fresh immutable subtree description for one mounted instance; invoked by the
/// instance's reactive render effect with the mount's rendering surface.
/// </summary>
/// <param name="frame">The per-mount cache and block-assembly surface.</param>
/// <returns>The rendered subtree, or null to render nothing.</returns>
public delegate VirtualNode? ComponentRenderer(ComponentRenderFrame frame);
