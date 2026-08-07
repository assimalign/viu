namespace Assimalign.Viu.Components;

/// <summary>
/// Authored component behavior: one instance per mounted component invocation. The static
/// identity and input/output declaration live on <see cref="ComponentRegistration"/>, not on the
/// instance, so the runtime reads the contract before activation.
/// </summary>
/// <remarks>Specified by <c>[CMP-1]</c>, <c>[CMP-8]</c>, and <c>[CMP-10]</c>.</remarks>
public interface IComponent
{
    /// <summary>
    /// Runs the instance's synchronous setup and returns the render closure invoked for the
    /// initial render and every reactive re-render. Setup is synchronous and the runtime invokes
    /// it inside the mount's reactive scope.
    /// </summary>
    /// <param name="context">The runtime-provided authoring surface for this mounted instance.</param>
    /// <returns>The render closure producing this instance's immutable subtree description.</returns>
    ComponentRenderer Setup(ComponentContext context);
}
