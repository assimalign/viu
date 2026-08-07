namespace Assimalign.Viu.Components;

/// <summary>
/// The composition body of a code-first component: runs once per mount with the authoring
/// surface and returns the render closure — the same shape a class-based
/// <see cref="IComponent.Setup"/> has, as a delegate.
/// </summary>
/// <param name="context">The runtime-provided authoring surface for this mounted instance.</param>
/// <returns>The render closure invoked for the initial render and every reactive re-render.</returns>
public delegate ComponentRenderer ComponentSetup(ComponentContext context);
