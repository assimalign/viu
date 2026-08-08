using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Buffers one server serialization and optionally drains it at subtree boundaries.</summary>
internal sealed class SsrWriter
{
    private readonly StringBuilder _builder = new();
    private readonly TextWriter? _sink;

    internal SsrWriter()
    {
    }

    internal SsrWriter(TextWriter sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
    }

    internal void Append(string chunk) => _builder.Append(chunk);

    internal async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_sink is null || _builder.Length == 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _sink.WriteAsync(
            _builder.ToString().AsMemory(),
            cancellationToken).ConfigureAwait(false);
        _builder.Clear();
        await _sink.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    internal string ToStringResult() => _builder.ToString();
}
