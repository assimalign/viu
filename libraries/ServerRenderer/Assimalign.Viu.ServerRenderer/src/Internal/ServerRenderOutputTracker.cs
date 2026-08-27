using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Tracks pending shell content while forwarding the host-owned output contract.</summary>
internal sealed class ServerRenderOutputTracker : IServerRenderOutput
{
    private readonly IServerRenderOutput _output;
    private bool _hasUnflushedContent;

    internal ServerRenderOutputTracker(IServerRenderOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;
    }

    public bool ResponseCommitted => _output.ResponseCommitted;

    public async ValueTask WriteAsync(
        ReadOnlyMemory<char> content,
        CancellationToken cancellationToken = default)
    {
        await _output.WriteAsync(content, cancellationToken).ConfigureAwait(false);
        if (content.Length > 0)
        {
            _hasUnflushedContent = true;
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
        _hasUnflushedContent = false;
    }

    internal ValueTask FlushPendingAsync(CancellationToken cancellationToken) =>
        _hasUnflushedContent
            ? FlushAsync(cancellationToken)
            : ValueTask.CompletedTask;
}
