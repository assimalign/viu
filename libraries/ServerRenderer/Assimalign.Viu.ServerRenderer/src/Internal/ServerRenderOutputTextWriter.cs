using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Bridges the host output contract into the serializer's existing text writer seam.</summary>
internal sealed class ServerRenderOutputTextWriter : TextWriter
{
    private readonly IServerRenderOutput _output;

    internal ServerRenderOutputTextWriter(IServerRenderOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override Task WriteAsync(
        ReadOnlyMemory<char> buffer,
        CancellationToken cancellationToken = default) =>
        _output.WriteAsync(buffer, cancellationToken).AsTask();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _output.FlushAsync(cancellationToken).AsTask();
}
