using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Assimalign.Viu.Testing.EndToEnd;

// [V01.01.12.30.05], #357: this is the Visual Studio side of the simulated
// BrowserRefresh topology. The real browser connects to the RunHost's downstream endpoint;
// this server accepts the RunHost's authenticated upstream connections.
internal sealed class VisualStudioBrowserRefreshStubServer : IAsyncDisposable
{
    private static readonly TimeSpan ProtocolTimeout = TimeSpan.FromSeconds(30);
    private readonly WebApplication _application;
    private readonly ConcurrentDictionary<long, WebSocket> _connections = [];
    private readonly object _connectionCountSynchronization = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly RSA _serverKey;
    private readonly ConcurrentQueue<string> _receivedMessages = [];
    private readonly SemaphoreSlim _receivedMessageAvailable = new(0);
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private TaskCompletionSource _connectionCountChanged = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _acceptedConnectionCount;
    private int _disposed;
    private long _lastConnectionIdentifier;

    private VisualStudioBrowserRefreshStubServer(
        WebApplication application,
        RSA serverKey,
        Uri address)
    {
        _application = application;
        _serverKey = serverKey;
        Address = address;
        PublicKey = Convert.ToBase64String(serverKey.ExportSubjectPublicKeyInfo());
    }

    internal Uri Address { get; }

    internal int AcceptedConnectionCount => Volatile.Read(
        ref _acceptedConnectionCount);

    internal string PublicKey { get; }

    internal static async Task<VisualStudioBrowserRefreshStubServer> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0));

        WebApplication application = builder.Build();
        application.UseWebSockets();
        RSA serverKey = RSA.Create(2048);
        VisualStudioBrowserRefreshStubServer? server = null;
        application.Map(
            "/browser-refresh",
            async context =>
            {
                if (server is null)
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    return;
                }

                await server.AcceptAsync(context);
            });

        try
        {
            await application.StartAsync();
            IServer hostingServer = application.Services.GetRequiredService<IServer>();
            IServerAddressesFeature addresses = hostingServer.Features
                .Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException(
                    "The BrowserRefresh stub did not expose its bound address.");
            string httpAddress = addresses.Addresses.Single();
            UriBuilder addressBuilder = new(httpAddress)
            {
                Scheme = Uri.UriSchemeWs,
                Path = "/browser-refresh",
            };
            server = new VisualStudioBrowserRefreshStubServer(
                application,
                serverKey,
                addressBuilder.Uri);
            return server;
        }
        catch
        {
            serverKey.Dispose();
            await application.DisposeAsync();
            throw;
        }
    }

    internal Task WaitForAuthenticatedConnectionAsync()
        => WaitForAuthenticatedConnectionCountAsync(1);

    internal async Task WaitForAuthenticatedConnectionCountAsync(
        int expectedCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedCount, 1);
        DateTime deadline = DateTime.UtcNow + ProtocolTimeout;
        while (true)
        {
            Task connectionCountChanged;
            lock (_connectionCountSynchronization)
            {
                if (_acceptedConnectionCount >= expectedCount)
                {
                    return;
                }

                connectionCountChanged = _connectionCountChanged.Task;
            }

            TimeSpan remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            try
            {
                await connectionCountChanged.WaitAsync(
                    remaining,
                    _stopping.Token);
            }
            catch (TimeoutException)
            {
                break;
            }
        }

        throw new TimeoutException(
            $"Timed out waiting for {expectedCount} authenticated Visual Studio "
            + "BrowserRefresh bridge connections.");
    }

    internal async Task SendAsync(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        await WaitForAuthenticatedConnectionAsync();
        byte[] content = Encoding.UTF8.GetBytes(message);
        await _sendLock.WaitAsync(_stopping.Token);
        try
        {
            foreach ((long connectionIdentifier, WebSocket connection) in
                _connections.ToArray())
            {
                if (connection.State != WebSocketState.Open)
                {
                    RemoveAndDisposeConnection(connectionIdentifier);
                    continue;
                }

                try
                {
                    await connection.SendAsync(
                        content,
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        _stopping.Token);
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException
                        or ObjectDisposedException
                        or WebSocketException)
                {
                    RemoveAndDisposeConnection(connectionIdentifier);
                }
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    internal async Task<string> WaitForReceivedMessageAsync(
        Func<string, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        DateTime deadline = DateTime.UtcNow + ProtocolTimeout;
        while (DateTime.UtcNow < deadline)
        {
            while (_receivedMessages.TryDequeue(out string? message))
            {
                if (predicate(message))
                {
                    return message;
                }
            }

            TimeSpan remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await _receivedMessageAvailable.WaitAsync(remaining, _stopping.Token);
        }

        throw new TimeoutException(
            "Timed out waiting for a response through the Visual Studio BrowserRefresh bridge.");
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stopping.Cancel();
        await _sendLock.WaitAsync();
        try
        {
            foreach (WebSocket connection in _connections.Values)
            {
                try
                {
                    if (connection.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    {
                        await connection.CloseOutputAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Harness shutdown",
                            CancellationToken.None);
                    }
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException
                        or ObjectDisposedException
                        or WebSocketException)
                {
                }
            }
        }
        finally
        {
            _sendLock.Release();
        }

        await _application.StopAsync();
        await _sendLock.WaitAsync();
        try
        {
            foreach (long connectionIdentifier in _connections.Keys.ToArray())
            {
                RemoveAndDisposeConnection(connectionIdentifier);
            }
        }
        finally
        {
            _sendLock.Release();
        }

        await _application.DisposeAsync();
        _sendLock.Dispose();
        _receivedMessageAvailable.Dispose();
        _stopping.Dispose();
        _serverKey.Dispose();
    }

    private async Task AcceptAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        string[] protocols = context.WebSockets.WebSocketRequestedProtocols.ToArray();
        if (protocols.Length != 1)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        string protocol = protocols[0];
        try
        {
            byte[] encryptedSecret = Convert.FromBase64String(
                Uri.UnescapeDataString(protocol));
            byte[] secret = _serverKey.Decrypt(
                encryptedSecret,
                RSAEncryptionPadding.OaepSHA256);
            if (secret.Length != 32)
            {
                throw new CryptographicException(
                    "The BrowserRefresh encrypted secret did not contain 32 bytes.");
            }
        }
        catch (Exception exception) when (
            exception is FormatException or CryptographicException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        WebSocket connection = await context.WebSockets.AcceptWebSocketAsync(protocol);
        long connectionIdentifier = Interlocked.Increment(
            ref _lastConnectionIdentifier);
        try
        {
            await _sendLock.WaitAsync(_stopping.Token);
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        try
        {
            if (!_connections.TryAdd(connectionIdentifier, connection))
            {
                connection.Dispose();
                throw new InvalidOperationException(
                    "The Visual Studio BrowserRefresh stub could not register an authenticated connection.");
            }
        }
        finally
        {
            _sendLock.Release();
        }

        RecordAcceptedConnection();
        try
        {
            await ReceiveLoopAsync(connection, _stopping.Token);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or ObjectDisposedException
                or OperationCanceledException
                or WebSocketException)
        {
        }
        finally
        {
            await _sendLock.WaitAsync();
            try
            {
                RemoveAndDisposeConnection(connectionIdentifier);
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }

    private void RecordAcceptedConnection()
    {
        TaskCompletionSource connectionCountChanged;
        lock (_connectionCountSynchronization)
        {
            _acceptedConnectionCount++;
            connectionCountChanged = _connectionCountChanged;
            _connectionCountChanged = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        connectionCountChanged.TrySetResult();
    }

    private void RemoveAndDisposeConnection(long connectionIdentifier)
    {
        if (_connections.TryRemove(
                connectionIdentifier,
                out WebSocket? removedConnection))
        {
            removedConnection.Dispose();
        }
    }

    private async Task ReceiveLoopAsync(
        WebSocket connection,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[16 * 1024];
        while (!cancellationToken.IsCancellationRequested
            && connection.State is WebSocketState.Open or WebSocketState.CloseSent)
        {
            using MemoryStream message = new();
            WebSocketReceiveResult result;
            do
            {
                result = await connection.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                _receivedMessages.Enqueue(
                    Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                _receivedMessageAvailable.Release();
            }
        }
    }
}
