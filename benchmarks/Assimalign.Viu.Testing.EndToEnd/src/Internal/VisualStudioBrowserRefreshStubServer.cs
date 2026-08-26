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
// this server accepts only the RunHost's authenticated upstream connection.
internal sealed class VisualStudioBrowserRefreshStubServer : IAsyncDisposable
{
    private static readonly TimeSpan ProtocolTimeout = TimeSpan.FromSeconds(30);
    private readonly WebApplication _application;
    private readonly CancellationTokenSource _stopping = new();
    private readonly RSA _serverKey;
    private readonly TaskCompletionSource<WebSocket> _connection = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<string> _receivedMessages = [];
    private readonly SemaphoreSlim _receivedMessageAvailable = new(0);
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private Task? _receiveLoop;

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

    internal async Task WaitForAuthenticatedConnectionAsync()
    {
        _ = await _connection.Task.WaitAsync(ProtocolTimeout);
    }

    internal async Task SendAsync(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        WebSocket connection = await _connection.Task.WaitAsync(ProtocolTimeout);
        byte[] content = Encoding.UTF8.GetBytes(message);
        await _sendLock.WaitAsync(_stopping.Token);
        try
        {
            await connection.SendAsync(
                content,
                WebSocketMessageType.Text,
                endOfMessage: true,
                _stopping.Token);
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
        _stopping.Cancel();
        if (_connection.Task.IsCompletedSuccessfully)
        {
            WebSocket connection = await _connection.Task;
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
            catch (WebSocketException)
            {
            }
        }

        await _application.StopAsync();
        if (_receiveLoop is not null)
        {
            try
            {
                await _receiveLoop;
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
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
            _connection.TrySetException(
                new InvalidOperationException(
                    "The RunHost BrowserRefresh client did not supply exactly one encrypted-secret protocol."));
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
            _connection.TrySetException(
                new InvalidOperationException(
                    "The RunHost BrowserRefresh client supplied an invalid encrypted secret.",
                    exception));
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        WebSocket connection = await context.WebSockets.AcceptWebSocketAsync(protocol);
        if (!_connection.TrySetResult(connection))
        {
            await connection.CloseAsync(
                WebSocketCloseStatus.PolicyViolation,
                "Only one upstream connection is accepted.",
                context.RequestAborted);
            return;
        }

        _receiveLoop = ReceiveLoopAsync(connection, _stopping.Token);
        await _receiveLoop;
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
