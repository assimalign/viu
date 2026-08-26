using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Assimalign.Viu.Sdk.Browser.RunHost;

internal sealed class BrowserRefreshBridge : IAsyncDisposable
{
    private static readonly TimeSpan UpstreamConnectionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SocketCloseTimeout = TimeSpan.FromSeconds(1);
    private readonly ConcurrentDictionary<long, BridgedBrowserConnection> _connections = new();
    private readonly ConcurrentDictionary<string, byte[]> _managedStylesheetUpdateMessages = new(
        StringComparer.Ordinal);
    private readonly CancellationTokenSource _shutdownCancellationSource = new();
    private readonly WebApplication _application;
    private readonly IReadOnlyList<Uri> _upstreamEndpoints;
    private readonly Action<long, string, string?>? _upstreamMessageRelayed;
    private readonly string _bridgePath;
    private long _lastConnectionIdentifier;
    private int _disposed;

    private BrowserRefreshBridge(
        WebApplication application,
        IReadOnlyList<Uri> upstreamEndpoints,
        string bridgePath,
        IReadOnlyList<string> managedStaticFilePaths,
        Action<long, string, string?>? upstreamMessageRelayed)
    {
        _application = application;
        _upstreamEndpoints = upstreamEndpoints;
        _bridgePath = bridgePath;
        _upstreamMessageRelayed = upstreamMessageRelayed;
        foreach (string path in managedStaticFilePaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            TrackManagedStylesheet(path, CreateStaticFileUpdateMessage(path));
        }
    }

    internal string ChildEndpointList { get; private set; } = string.Empty;

    internal int ConnectedBrowserCount => _connections.Count;

    internal string Endpoint { get; private set; } = string.Empty;

    internal static async Task<BrowserRefreshBridge> StartAsync(
        string upstreamEndpointList,
        CancellationToken cancellationToken = default)
        => await StartAsync(
            upstreamEndpointList,
            Array.Empty<string>(),
            upstreamMessageRelayed: null,
            cancellationToken);

    internal static async Task<BrowserRefreshBridge> StartAsync(
        string upstreamEndpointList,
        IReadOnlyList<string> managedStaticFilePaths,
        Action<long, string, string?>? upstreamMessageRelayed = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(managedStaticFilePaths);
        (IReadOnlyList<Uri> endpoints, string normalizedEndpointList) =
            ParseEndpointList(upstreamEndpointList);
        string bridgePath = "/viu-browser-refresh/" + Guid.NewGuid().ToString("N");
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(
            options => options.Listen(IPAddress.Loopback, 0));

        WebApplication application = builder.Build();
        BrowserRefreshBridge bridge = new(
            application,
            endpoints,
            bridgePath,
            managedStaticFilePaths,
            upstreamMessageRelayed);
        application.UseWebSockets();
        application.Run(bridge.HandleRequestAsync);

        try
        {
            await application.StartAsync(cancellationToken);
            string serverAddress = application.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()?
                .Addresses
                .SingleOrDefault()
                ?? throw new InvalidOperationException(
                    "The BrowserRefresh bridge did not publish its loopback endpoint.");
            UriBuilder bridgeEndpoint = new(serverAddress)
            {
                Scheme = Uri.UriSchemeWs,
                Path = bridgePath,
            };
            bridge.Endpoint = bridgeEndpoint.Uri.AbsoluteUri;
            bridge.ChildEndpointList =
                bridge.Endpoint + "," + normalizedEndpointList;
            return bridge;
        }
        catch
        {
            await bridge.DisposeAsync();
            throw;
        }
    }

    internal async ValueTask<int> BroadcastStaticFileUpdateAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byte[] message = CreateStaticFileUpdateMessage(path);
        TrackManagedStylesheet(path, message);
        int deliveredCount = 0;
        foreach ((long connectionIdentifier, BridgedBrowserConnection connection) in
            _connections.ToArray())
        {
            if (await connection.SendDownstreamAsync(message, cancellationToken))
            {
                deliveredCount++;
                continue;
            }

            if (_connections.TryRemove(connectionIdentifier, out BridgedBrowserConnection? removed))
            {
                await removed.DisposeAsync();
            }
        }

        return deliveredCount;
    }

    internal async ValueTask SendUpdateStaticFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        _ = await BroadcastStaticFileUpdateAsync(path, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        BridgedBrowserConnection[] connections = _connections.Values.ToArray();
        _connections.Clear();
        foreach (BridgedBrowserConnection connection in connections)
        {
            await connection.DisposeAsync();
        }

        _shutdownCancellationSource.Cancel();
        try
        {
            await _application.StopAsync();
        }
        finally
        {
            await _application.DisposeAsync();
            _shutdownCancellationSource.Dispose();
        }
    }

    private static (
        IReadOnlyList<Uri> Endpoints,
        string NormalizedEndpointList) ParseEndpointList(string endpointList)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointList);
        string[] endpointTexts = endpointList.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (endpointTexts.Length == 0)
        {
            throw new ArgumentException(
                "At least one BrowserRefresh WebSocket endpoint is required.",
                nameof(endpointList));
        }

        List<Uri> endpoints = new(endpointTexts.Length);
        foreach (string endpointText in endpointTexts)
        {
            if (!Uri.TryCreate(endpointText, UriKind.Absolute, out Uri? endpoint)
                || (endpoint.Scheme != Uri.UriSchemeWs
                    && endpoint.Scheme != Uri.UriSchemeWss))
            {
                throw new ArgumentException(
                    "BrowserRefresh endpoints must use the ws or wss scheme.",
                    nameof(endpointList));
            }

            endpoints.Add(endpoint);
        }

        return (endpoints, string.Join(',', endpointTexts));
    }

    private static byte[] CreateStaticFileUpdateMessage(string path)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        writer.WriteString("type", "UpdateStaticFile");
        writer.WriteString("path", path);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private void TrackManagedStylesheet(string path, byte[] message)
    {
        // [V01.01.12.30.05], #357: the stock BrowserRefresh client reloads the document for
        // non-CSS UpdateStaticFile paths. Replaying those paths on every connection would create
        // a reload loop, while CSS paths are idempotent cache-busted stylesheet replacements.
        if (path.EndsWith(".css", StringComparison.Ordinal))
        {
            _managedStylesheetUpdateMessages.TryAdd(path, message);
        }
    }

    private async Task HandleRequestAsync(HttpContext context)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        if (context.Request.Path != _bridgePath
            || !context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        IList<string> requestedSubProtocols =
            context.WebSockets.WebSocketRequestedProtocols;
        if (requestedSubProtocols.Count > 1)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        string? requestedSubProtocol = requestedSubProtocols.Count == 1
            ? requestedSubProtocols[0]
            : null;
        using CancellationTokenSource requestCancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted,
                _shutdownCancellationSource.Token);
        ClientWebSocket? upstreamSocket = await ConnectUpstreamAsync(
            requestedSubProtocol,
            requestCancellationSource.Token);
        if (upstreamSocket is null)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        WebSocket? downstreamSocket = null;
        BridgedBrowserConnection? connection = null;
        long connectionIdentifier = 0;
        try
        {
            downstreamSocket = await context.WebSockets.AcceptWebSocketAsync(
                requestedSubProtocol);
            connectionIdentifier = Interlocked.Increment(
                ref _lastConnectionIdentifier);
            connection = new BridgedBrowserConnection(
                upstreamSocket,
                downstreamSocket,
                SocketCloseTimeout,
                _upstreamMessageRelayed is null
                    ? null
                    : (messageType, path) => _upstreamMessageRelayed(
                        connectionIdentifier,
                        messageType,
                        path));
            upstreamSocket = null;
            downstreamSocket = null;
            if (!_connections.TryAdd(connectionIdentifier, connection))
            {
                throw new InvalidOperationException(
                    "The BrowserRefresh bridge could not register a browser connection.");
            }

            byte[][] synchronizationMessages = _managedStylesheetUpdateMessages
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Value)
                .ToArray();
            await connection.RunAsync(
                synchronizationMessages,
                requestCancellationSource.Token);
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidOperationException
                or OperationCanceledException
                or WebSocketException)
        {
        }
        finally
        {
            if (connection is not null)
            {
                _connections.TryRemove(connectionIdentifier, out _);
                await connection.DisposeAsync();
            }

            downstreamSocket?.Dispose();
            upstreamSocket?.Dispose();
        }
    }

    private async Task<ClientWebSocket?> ConnectUpstreamAsync(
        string? requestedSubProtocol,
        CancellationToken cancellationToken)
    {
        foreach (Uri upstreamEndpoint in _upstreamEndpoints)
        {
            ClientWebSocket socket = new();
            if (requestedSubProtocol is not null)
            {
                socket.Options.AddSubProtocol(requestedSubProtocol);
            }

            using CancellationTokenSource connectionCancellationSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectionCancellationSource.CancelAfter(UpstreamConnectionTimeout);
            try
            {
                await socket.ConnectAsync(
                    upstreamEndpoint,
                    connectionCancellationSource.Token);
                return socket;
            }
            catch (Exception exception) when (
                exception is HttpRequestException
                    or IOException
                    or WebSocketException
                    || exception is OperationCanceledException
                        && !cancellationToken.IsCancellationRequested)
            {
                socket.Dispose();
            }
        }

        return null;
    }

    private sealed class BridgedBrowserConnection : IAsyncDisposable
    {
        private readonly CancellationTokenSource _shutdownCancellationSource = new();
        private readonly SemaphoreSlim _downstreamSendSemaphore = new(1, 1);
        private readonly TaskCompletionSource _disposeCompletionSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _relayCompletionSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim _upstreamSendSemaphore = new(1, 1);
        private readonly WebSocket _downstreamSocket;
        private readonly WebSocket _upstreamSocket;
        private readonly Action<string, string?>? _upstreamMessageRelayed;
        private readonly TimeSpan _socketCloseTimeout;
        private int _disposed;
        private int _gracefulCloseStarted;

        internal BridgedBrowserConnection(
            WebSocket upstreamSocket,
            WebSocket downstreamSocket,
            TimeSpan socketCloseTimeout,
            Action<string, string?>? upstreamMessageRelayed)
        {
            _upstreamSocket = upstreamSocket;
            _downstreamSocket = downstreamSocket;
            _socketCloseTimeout = socketCloseTimeout;
            _upstreamMessageRelayed = upstreamMessageRelayed;
        }

        internal async Task RunAsync(
            IReadOnlyList<byte[]> synchronizationMessages,
            CancellationToken cancellationToken)
        {
            try
            {
                foreach (byte[] message in synchronizationMessages)
                {
                    if (!await SendDownstreamAsync(message, cancellationToken))
                    {
                        return;
                    }
                }

                using CancellationTokenSource relayCancellationSource =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        _shutdownCancellationSource.Token);
                Task upstreamRelay = RelayMessagesAsync(
                    _upstreamSocket,
                    _downstreamSocket,
                    _downstreamSendSemaphore,
                    _upstreamMessageRelayed,
                    relayCancellationSource.Token);
                Task downstreamRelay = RelayMessagesAsync(
                    _downstreamSocket,
                    _upstreamSocket,
                    _upstreamSendSemaphore,
                    messageRelayed: null,
                    relayCancellationSource.Token);

                await Task.WhenAny(upstreamRelay, downstreamRelay);
                if (Volatile.Read(ref _gracefulCloseStarted) == 0)
                {
                    relayCancellationSource.Cancel();
                }

                await ObserveRelayCompletionAsync(upstreamRelay);
                await ObserveRelayCompletionAsync(downstreamRelay);
            }
            finally
            {
                _relayCompletionSource.TrySetResult();
            }
        }

        internal async ValueTask<bool> SendDownstreamAsync(
            ReadOnlyMemory<byte> message,
            CancellationToken cancellationToken)
        {
            using CancellationTokenSource sendCancellationSource =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _shutdownCancellationSource.Token);
            try
            {
                await SendMessageAsync(
                    _downstreamSocket,
                    _downstreamSendSemaphore,
                    message,
                    WebSocketMessageType.Text,
                    sendCancellationSource.Token);
                return true;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or ObjectDisposedException
                    or WebSocketException
                    || exception is OperationCanceledException
                        && !cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                await _disposeCompletionSource.Task;
                return;
            }

            try
            {
                Volatile.Write(ref _gracefulCloseStarted, 1);
                using CancellationTokenSource closeCancellationSource = new(
                    _socketCloseTimeout);
                await Task.WhenAll(
                    CloseOutputAsync(
                        _downstreamSocket,
                        _downstreamSendSemaphore,
                        closeCancellationSource.Token),
                    CloseOutputAsync(
                        _upstreamSocket,
                        _upstreamSendSemaphore,
                        closeCancellationSource.Token));
                try
                {
                    await _relayCompletionSource.Task.WaitAsync(
                        closeCancellationSource.Token);
                }
                catch (OperationCanceledException)
                {
                    _shutdownCancellationSource.Cancel();
                    await _relayCompletionSource.Task;
                }

                _shutdownCancellationSource.Cancel();
                _downstreamSocket.Dispose();
                _upstreamSocket.Dispose();
                _shutdownCancellationSource.Dispose();
                _downstreamSendSemaphore.Dispose();
                _upstreamSendSemaphore.Dispose();
                _disposeCompletionSource.TrySetResult();
            }
            catch (Exception exception)
            {
                _disposeCompletionSource.TrySetException(exception);
                throw;
            }
        }

        private static async Task RelayMessagesAsync(
            WebSocket source,
            WebSocket destination,
            SemaphoreSlim destinationSendSemaphore,
            Action<string, string?>? messageRelayed,
            CancellationToken cancellationToken)
        {
            ArrayBufferWriter<byte> message = new(4096);
            while (!cancellationToken.IsCancellationRequested)
            {
                message.Clear();
                WebSocketMessageType? messageType = null;
                while (true)
                {
                    Memory<byte> receiveBuffer = message.GetMemory(4096);
                    ValueWebSocketReceiveResult result = await source.ReceiveAsync(
                        receiveBuffer,
                        cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    messageType ??= result.MessageType;
                    if (messageType != result.MessageType)
                    {
                        throw new InvalidDataException(
                            "A BrowserRefresh WebSocket message changed type between fragments.");
                    }

                    message.Advance(result.Count);
                    if (result.EndOfMessage)
                    {
                        break;
                    }
                }

                await SendMessageAsync(
                    destination,
                    destinationSendSemaphore,
                    message.WrittenMemory,
                    messageType.GetValueOrDefault(),
                    cancellationToken);
                if (messageRelayed is not null
                    && messageType == WebSocketMessageType.Text
                    && TryReadMessage(
                        message.WrittenSpan,
                        out string? relayedMessageType,
                        out string? relayedPath))
                {
                    messageRelayed(relayedMessageType, relayedPath);
                }
            }
        }

        private static bool TryReadMessage(
            ReadOnlySpan<byte> message,
            out string messageType,
            out string? path)
        {
            messageType = string.Empty;
            path = null;
            try
            {
                Utf8JsonReader reader = new(
                    message,
                    isFinalBlock: true,
                    state: default);
                while (reader.Read())
                {
                    if (reader.TokenType != JsonTokenType.PropertyName
                        || reader.CurrentDepth != 1)
                    {
                        continue;
                    }

                    bool isType = reader.ValueTextEquals("type"u8);
                    bool isPath = reader.ValueTextEquals("path"u8);
                    if ((!isType && !isPath)
                        || !reader.Read()
                        || reader.TokenType != JsonTokenType.String)
                    {
                        continue;
                    }

                    if (isType)
                    {
                        messageType = reader.GetString() ?? string.Empty;
                    }
                    else
                    {
                        path = reader.GetString();
                    }
                }
            }
            catch (JsonException)
            {
            }

            return messageType.Length > 0;
        }

        private static async ValueTask SendMessageAsync(
            WebSocket destination,
            SemaphoreSlim sendSemaphore,
            ReadOnlyMemory<byte> message,
            WebSocketMessageType messageType,
            CancellationToken cancellationToken)
        {
            await sendSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (destination.State != WebSocketState.Open)
                {
                    throw new InvalidOperationException(
                        "The BrowserRefresh WebSocket is not open.");
                }

                await destination.SendAsync(
                    message,
                    messageType,
                    endOfMessage: true,
                    cancellationToken);
            }
            finally
            {
                sendSemaphore.Release();
            }
        }

        private static async Task CloseOutputAsync(
            WebSocket socket,
            SemaphoreSlim sendSemaphore,
            CancellationToken cancellationToken)
        {
            try
            {
                await sendSemaphore.WaitAsync(cancellationToken);
                try
                {
                    if (socket.State is WebSocketState.Open
                        or WebSocketState.CloseReceived)
                    {
                        await socket.CloseOutputAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "BrowserRefresh bridge stopped.",
                            cancellationToken);
                    }
                }
                finally
                {
                    sendSemaphore.Release();
                }
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or ObjectDisposedException
                    or OperationCanceledException
                    or WebSocketException)
            {
            }
        }

        private static async Task ObserveRelayCompletionAsync(Task relay)
        {
            try
            {
                await relay;
            }
            catch (Exception exception) when (
                exception is IOException
                    or InvalidOperationException
                    or ObjectDisposedException
                    or OperationCanceledException
                    or WebSocketException)
            {
            }
        }
    }
}
