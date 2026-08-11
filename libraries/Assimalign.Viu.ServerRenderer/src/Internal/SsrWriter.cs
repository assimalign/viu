using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Buffers one server serialization and optionally drains it at subtree boundaries.</summary>
internal sealed class SsrWriter
{
    private readonly StringBuilder _builder = new();
    private readonly List<string>? _recordedFlushChunks;
    private readonly TextWriter? _sink;

    internal SsrWriter()
    {
    }

    internal SsrWriter(bool recordFlushBoundaries)
    {
        if (recordFlushBoundaries)
        {
            _recordedFlushChunks = [];
        }
    }

    internal SsrWriter(TextWriter sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
    }

    internal void Append(string chunk) => _builder.Append(chunk);

    internal IReadOnlyList<string> RecordedFlushChunks =>
        _recordedFlushChunks is null
            ? Array.Empty<string>()
            : _recordedFlushChunks;

    internal async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_recordedFlushChunks is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_builder.Length == 0)
            {
                return;
            }

            _recordedFlushChunks.Add(_builder.ToString());
            _builder.Clear();
            return;
        }

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
