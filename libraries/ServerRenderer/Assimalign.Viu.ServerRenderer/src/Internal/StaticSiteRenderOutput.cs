using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

internal sealed class StaticSiteRenderOutput : IServerRenderOutput
{
    private readonly StringBuilder _document = new();

    // All accepted writes can still be discarded until the complete render and teardown succeed.
    public bool ResponseCommitted => false;

    public ValueTask WriteAsync(ReadOnlyMemory<char> content, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _document.Append(content.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public override string ToString() => _document.ToString();
}
