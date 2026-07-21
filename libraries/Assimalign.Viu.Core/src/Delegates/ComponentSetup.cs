namespace Assimalign.Viu;

/// <summary>
/// The render function a component's <see cref="IComponent.Setup"/> returns — re-invoked per update
/// to produce the component's subtree (null renders a placeholder comment). The C# port of the
/// render function returned by Vue's <c>setup()</c>
/// (https://vuejs.org/api/composition-api-setup.html#usage-with-render-functions): state lives in
/// the closure the setup body built, so each invocation re-reads the reactive values it captured
/// and the render effect re-tracks them.
/// </summary>
public delegate VirtualNode? ComponentSetup();
