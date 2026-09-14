using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools.Client;

/// <summary>Carries complete protocol JSON frames without exposing runtime objects. Specified by <c>[DVT-14]</c>.</summary>
/// <remarks>Single-threaded; implementations deliver callbacks on the owning event loop.</remarks>
public interface IDevToolsClientTransport : IAsyncDisposable
{
    /// <summary>Starts reception; connected requests a fresh handshake on initial connection and each reconnect.</summary>
    ValueTask StartAsync(Func<string, ValueTask> receiver, Action connected, CancellationToken cancellationToken = default);

    /// <summary>Sends one complete batch. The session never calls this from a receive callback.</summary>
    ValueTask SendAsync(string message, CancellationToken cancellationToken = default);
}
