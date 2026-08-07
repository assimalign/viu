using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Serializes the generic virtual tree as HTML while using Core's render scope for components.
/// </summary>
/// <remarks>
/// With scoped CSS deferred, the serializer reads no style-scope state and emits no scope
/// attributes.
/// </remarks>
public sealed class ServerRenderer
{
    private readonly ServerMarkupSerializer _serializer;

    /// <summary>Initializes an HTML server renderer over one explicitly composed component host.</summary>
    /// <param name="componentHost">The Core component execution facade.</param>
    public ServerRenderer(ComponentHost componentHost)
    {
        ArgumentNullException.ThrowIfNull(componentHost);
        _serializer = new ServerMarkupSerializer(componentHost);
    }

    /// <summary>Renders one component invocation and every nested invocation to a text writer.</summary>
    /// <param name="component">The immutable root invocation.</param>
    /// <param name="writer">The externally owned destination.</param>
    /// <param name="cancellationToken">Cancellation for component prefetch and writer operations.</param>
    public ValueTask RenderAsync(
        ComponentNode component,
        TextWriter writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(writer);
        return _serializer.WriteAsync(component, writer, null, cancellationToken);
    }
}
