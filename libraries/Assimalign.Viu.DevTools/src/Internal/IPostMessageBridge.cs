using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools;

internal interface IPostMessageBridge : IAsyncDisposable
{
    ValueTask StartAsync(
        Func<string, ValueTask> receiver,
        CancellationToken cancellationToken);

    ValueTask SendAsync(string message, CancellationToken cancellationToken);
}
