using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Pairs one component identity with its compiler-produced direct server render.
/// </summary>
/// <remarks>
/// The registration is immutable and contains no discovered metadata. Its reference is the same
/// identity carried by the ordinary <see cref="ComponentRegistration"/>, keeping activation and
/// server rendering in one explicit catalog. Specified by <c>[SSR-TARGET-2]</c>.
/// </remarks>
public sealed class ServerRenderRegistration
{
    /// <summary>Initializes an immutable server-render registration.</summary>
    /// <param name="reference">The component identity carried by virtual component nodes.</param>
    /// <param name="render">The statically generated render delegate.</param>
    public ServerRenderRegistration(
        ComponentReference reference,
        CompiledServerComponentRender render)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(render);
        Reference = reference;
        Render = render;
    }

    /// <summary>Gets the component identity this server body renders.</summary>
    public ComponentReference Reference { get; }

    /// <summary>Gets the statically generated direct-markup render delegate.</summary>
    public CompiledServerComponentRender Render { get; }
}
