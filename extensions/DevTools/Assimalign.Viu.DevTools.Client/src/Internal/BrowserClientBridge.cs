using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools.Client;

[SupportedOSPlatform("browser")]
internal sealed partial class BrowserClientBridge : IDevToolsClientTransport
{
    private const string ModuleName = "Assimalign.Viu.DevTools.Client";
    private readonly string _address;
    private readonly bool _socket;
    private int _subscription;
    private bool _disposed;

    internal BrowserClientBridge(string address, bool socket) { _address = address; _socket = socket; }

    public async ValueTask StartAsync(Func<string, ValueTask> receiver, Action connected, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_subscription != 0)
        {
            return;
        }

        await JSHost.ImportAsync(ModuleName, "/_content/Assimalign.Viu.DevTools.Client/viu-devtools-client.js", cancellationToken);
        _subscription = Subscribe(_address, _socket, message => { _ = receiver(message); }, connected);
    }
    public ValueTask SendAsync(string message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        Send(_subscription, message);
        return ValueTask.CompletedTask;
    }
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        if (_subscription != 0)
        {
            Unsubscribe(_subscription);
        }

        _subscription = 0;
        return ValueTask.CompletedTask;
    }

    [JSImport("subscribe", ModuleName)]
    private static partial int Subscribe(string address, bool socket,
        [JSMarshalAs<JSType.Function<JSType.String>>] Action<string> receiver,
        [JSMarshalAs<JSType.Function>] Action connected);
    [JSImport("send", ModuleName)]
    private static partial void Send(int subscription, string message);
    [JSImport("unsubscribe", ModuleName)]
    private static partial void Unsubscribe(int subscription);
}
