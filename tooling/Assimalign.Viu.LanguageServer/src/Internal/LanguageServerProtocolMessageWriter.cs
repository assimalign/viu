using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.LanguageServer;

internal sealed class LanguageServerProtocolMessageWriter
{
    private readonly Stream output;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    internal LanguageServerProtocolMessageWriter(Stream output)
        => this.output = output ?? throw new ArgumentNullException(nameof(output));

    internal async ValueTask WriteAsync(
        JsonObject message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payload = Encoding.UTF8.GetBytes(message.ToJsonString());
        var header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");

        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
