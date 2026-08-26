using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Sdk.Browser.RunHost.Tests;

public sealed class BrowserRefreshBridgeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task StartAsync_ValidEndpoints_PublishesLoopbackEndpointBeforeOriginalFallbacks()
    {
        await using BrowserRefreshBridge bridge = await BrowserRefreshBridge.StartAsync(
            " ws://127.0.0.1:40101/first , wss://example.invalid/second ");

        string[] endpoints = bridge.ChildEndpointList.Split(',');
        Uri localEndpoint = new(endpoints[0]);

        endpoints.Length.ShouldBe(3);
        endpoints[0].ShouldBe(bridge.Endpoint);
        localEndpoint.Scheme.ShouldBe(Uri.UriSchemeWs);
        localEndpoint.Host.ShouldBe(IPAddress.Loopback.ToString());
        localEndpoint.Port.ShouldBeGreaterThan(0);
        endpoints[1].ShouldBe("ws://127.0.0.1:40101/first");
        endpoints[2].ShouldBe("wss://example.invalid/second");
    }

    [Fact]
    public async Task Connection_FragmentedMessages_RelaysCompleteMessagesAndExactSubProtocol()
    {
        using CancellationTokenSource cancellationSource = new(TestTimeout);
        await using BrowserRefreshServerStub upstream =
            await BrowserRefreshServerStub.StartAsync(cancellationSource.Token);
        await using BrowserRefreshBridge bridge = await BrowserRefreshBridge.StartAsync(
            "ws://127.0.0.1:1/unavailable," + upstream.Endpoint.AbsoluteUri,
            cancellationSource.Token);
        using ClientWebSocket browser = new();
        const string requestedSubProtocol = "encrypted%2Bsecret%2Fvalue%3D";
        browser.Options.AddSubProtocol(requestedSubProtocol);

        await browser.ConnectAsync(
            new Uri(bridge.Endpoint),
            cancellationSource.Token);
        await using BrowserRefreshServerStubConnection upstreamConnection =
            await upstream.AcceptConnectionAsync(cancellationSource.Token);

        upstreamConnection.RequestedSubProtocol.ShouldBe(requestedSubProtocol);
        browser.SubProtocol.ShouldBe(requestedSubProtocol);
        await upstreamConnection.SendTextFragmentsAsync(
            ["upstream-", "message"],
            cancellationSource.Token);
        ReceivedWebSocketMessage browserMessage = await ReceiveMessageAsync(
            browser,
            cancellationSource.Token);
        await SendTextFragmentsAsync(
            browser,
            ["browser-", "response"],
            cancellationSource.Token);
        ReceivedWebSocketMessage upstreamMessage =
            await upstreamConnection.ReceiveMessageAsync(cancellationSource.Token);

        browserMessage.Text.ShouldBe("upstream-message");
        browserMessage.FragmentCount.ShouldBe(1);
        browserMessage.MessageType.ShouldBe(WebSocketMessageType.Text);
        upstreamMessage.Text.ShouldBe("browser-response");
        upstreamMessage.FragmentCount.ShouldBe(1);
        upstreamMessage.MessageType.ShouldBe(WebSocketMessageType.Text);
    }

    [Fact]
    public async Task BroadcastStaticFileUpdateAsync_TwoBrowsers_SendsExactClientMessageToBoth()
    {
        using CancellationTokenSource cancellationSource = new(TestTimeout);
        await using BrowserRefreshServerStub upstream =
            await BrowserRefreshServerStub.StartAsync(cancellationSource.Token);
        await using BrowserRefreshBridge bridge = await BrowserRefreshBridge.StartAsync(
            upstream.Endpoint.AbsoluteUri,
            cancellationSource.Token);
        using ClientWebSocket firstBrowser = new();
        using ClientWebSocket secondBrowser = new();

        await firstBrowser.ConnectAsync(
            new Uri(bridge.Endpoint),
            cancellationSource.Token);
        await using BrowserRefreshServerStubConnection firstUpstreamConnection =
            await upstream.AcceptConnectionAsync(cancellationSource.Token);
        await secondBrowser.ConnectAsync(
            new Uri(bridge.Endpoint),
            cancellationSource.Token);
        await using BrowserRefreshServerStubConnection secondUpstreamConnection =
            await upstream.AcceptConnectionAsync(cancellationSource.Token);
        await WaitForConnectionCountAsync(
            bridge,
            expectedCount: 2,
            cancellationSource.Token);

        int deliveredCount = await bridge.BroadcastStaticFileUpdateAsync(
            "/site.css",
            cancellationSource.Token);
        ReceivedWebSocketMessage firstMessage = await ReceiveMessageAsync(
            firstBrowser,
            cancellationSource.Token);
        ReceivedWebSocketMessage secondMessage = await ReceiveMessageAsync(
            secondBrowser,
            cancellationSource.Token);

        deliveredCount.ShouldBe(2);
        firstMessage.Text.ShouldBe(
            "{\"type\":\"UpdateStaticFile\",\"path\":\"/site.css\"}");
        secondMessage.Text.ShouldBe(firstMessage.Text);
    }

    [Fact]
    public async Task DisposeAsync_ConnectedBrowser_CompletesCloseHandshakesAndRejectsLaterBroadcasts()
    {
        using CancellationTokenSource cancellationSource = new(TestTimeout);
        await using BrowserRefreshServerStub upstream =
            await BrowserRefreshServerStub.StartAsync(cancellationSource.Token);
        BrowserRefreshBridge bridge = await BrowserRefreshBridge.StartAsync(
            upstream.Endpoint.AbsoluteUri,
            cancellationSource.Token);
        using ClientWebSocket browser = new();

        await browser.ConnectAsync(
            new Uri(bridge.Endpoint),
            cancellationSource.Token);
        await using BrowserRefreshServerStubConnection upstreamConnection =
            await upstream.AcceptConnectionAsync(cancellationSource.Token);
        await WaitForConnectionCountAsync(
            bridge,
            expectedCount: 1,
            cancellationSource.Token);

        Task<ReceivedWebSocketMessage> browserCloseTask =
            ReceiveCloseAndAcknowledgeAsync(
                browser,
                cancellationSource.Token);
        Task<ReceivedWebSocketMessage> upstreamCloseTask =
            upstreamConnection.ReceiveCloseAndAcknowledgeAsync(
                cancellationSource.Token);
        await bridge.DisposeAsync();
        ReceivedWebSocketMessage browserClose = await browserCloseTask;
        ReceivedWebSocketMessage upstreamClose = await upstreamCloseTask;
        Func<Task> broadcast = async () =>
            await bridge.SendUpdateStaticFileAsync(
                "/after-disposal.css",
                cancellationSource.Token);

        bridge.ConnectedBrowserCount.ShouldBe(0);
        browserClose.MessageType.ShouldBe(WebSocketMessageType.Close);
        upstreamClose.MessageType.ShouldBe(WebSocketMessageType.Close);
        await broadcast.ShouldThrowAsync<ObjectDisposedException>();
        await bridge.DisposeAsync();
    }

    private static async Task WaitForConnectionCountAsync(
        BrowserRefreshBridge bridge,
        int expectedCount,
        CancellationToken cancellationToken)
    {
        while (bridge.ConnectedBrowserCount != expectedCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken);
        }
    }

    private static async Task SendTextFragmentsAsync(
        WebSocket socket,
        IReadOnlyList<string> fragments,
        CancellationToken cancellationToken)
    {
        for (int index = 0; index < fragments.Count; index++)
        {
            byte[] fragment = Encoding.UTF8.GetBytes(fragments[index]);
            await socket.SendAsync(
                fragment,
                WebSocketMessageType.Text,
                endOfMessage: index == fragments.Count - 1,
                cancellationToken);
        }
    }

    private static async Task<ReceivedWebSocketMessage> ReceiveMessageAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        ArrayBufferWriter<byte> message = new(1024);
        int fragmentCount = 0;
        WebSocketMessageType? messageType = null;
        while (true)
        {
            Memory<byte> buffer = message.GetMemory(1024);
            ValueWebSocketReceiveResult result = await socket.ReceiveAsync(
                buffer,
                cancellationToken);
            fragmentCount++;
            messageType ??= result.MessageType;
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return new ReceivedWebSocketMessage(
                    string.Empty,
                    result.MessageType,
                    fragmentCount);
            }

            message.Advance(result.Count);
            if (result.EndOfMessage)
            {
                return new ReceivedWebSocketMessage(
                    Encoding.UTF8.GetString(message.WrittenSpan),
                    messageType.GetValueOrDefault(),
                    fragmentCount);
            }
        }
    }

    private static async Task<ReceivedWebSocketMessage> ReceiveCloseAndAcknowledgeAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        ReceivedWebSocketMessage message = await ReceiveMessageAsync(
            socket,
            cancellationToken);
        if (message.MessageType == WebSocketMessageType.Close
            && socket.State == WebSocketState.CloseReceived)
        {
            await socket.CloseOutputAsync(
                WebSocketCloseStatus.NormalClosure,
                "Test close acknowledged.",
                cancellationToken);
        }

        return message;
    }

    private readonly record struct ReceivedWebSocketMessage(
        string Text,
        WebSocketMessageType MessageType,
        int FragmentCount);

    private sealed class BrowserRefreshServerStub : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<long, BrowserRefreshServerStubConnection>
            _connections = new();
        private readonly Channel<BrowserRefreshServerStubConnection> _acceptedConnections =
            Channel.CreateUnbounded<BrowserRefreshServerStubConnection>();
        private readonly WebApplication _application;
        private readonly string _path;
        private long _lastConnectionIdentifier;

        private BrowserRefreshServerStub(
            WebApplication application,
            string path)
        {
            _application = application;
            _path = path;
        }

        internal Uri Endpoint { get; private set; } = null!;

        internal static async Task<BrowserRefreshServerStub> StartAsync(
            CancellationToken cancellationToken)
        {
            string path = "/upstream/" + Guid.NewGuid().ToString("N");
            WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(
                options => options.Listen(IPAddress.Loopback, 0));
            WebApplication application = builder.Build();
            BrowserRefreshServerStub server = new(application, path);
            application.UseWebSockets();
            application.Run(server.HandleRequestAsync);
            await application.StartAsync(cancellationToken);
            string address = application.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()?
                .Addresses
                .SingleOrDefault()
                ?? throw new InvalidOperationException(
                    "The BrowserRefresh test server did not publish an endpoint.");
            UriBuilder endpoint = new(address)
            {
                Scheme = Uri.UriSchemeWs,
                Path = path,
            };
            server.Endpoint = endpoint.Uri;
            return server;
        }

        internal async Task<BrowserRefreshServerStubConnection> AcceptConnectionAsync(
            CancellationToken cancellationToken) =>
            await _acceptedConnections.Reader.ReadAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            _acceptedConnections.Writer.TryComplete();
            foreach (BrowserRefreshServerStubConnection connection in _connections.Values)
            {
                await connection.DisposeAsync();
            }

            _connections.Clear();
            try
            {
                await _application.StopAsync();
            }
            finally
            {
                await _application.DisposeAsync();
            }
        }

        private async Task HandleRequestAsync(HttpContext context)
        {
            if (context.Request.Path != _path
                || !context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            IList<string> requestedSubProtocols =
                context.WebSockets.WebSocketRequestedProtocols;
            string? requestedSubProtocol = requestedSubProtocols.Count == 1
                ? requestedSubProtocols[0]
                : null;
            WebSocket socket = await context.WebSockets.AcceptWebSocketAsync(
                requestedSubProtocol);
            long connectionIdentifier = Interlocked.Increment(
                ref _lastConnectionIdentifier);
            BrowserRefreshServerStubConnection connection = new(
                socket,
                requestedSubProtocol);
            _connections.TryAdd(connectionIdentifier, connection);
            await _acceptedConnections.Writer.WriteAsync(
                connection,
                context.RequestAborted);
            try
            {
                await connection.Completion.WaitAsync(context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _connections.TryRemove(connectionIdentifier, out _);
                await connection.DisposeAsync();
            }
        }
    }

    private sealed class BrowserRefreshServerStubConnection : IAsyncDisposable
    {
        private readonly TaskCompletionSource _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly WebSocket _socket;
        private int _disposed;

        internal BrowserRefreshServerStubConnection(
            WebSocket socket,
            string? requestedSubProtocol)
        {
            _socket = socket;
            RequestedSubProtocol = requestedSubProtocol;
        }

        internal Task Completion => _completion.Task;

        internal string? RequestedSubProtocol { get; }

        internal async Task<ReceivedWebSocketMessage> ReceiveMessageAsync(
            CancellationToken cancellationToken) =>
            await BrowserRefreshBridgeTests.ReceiveMessageAsync(
                _socket,
                cancellationToken);

        internal async Task<ReceivedWebSocketMessage> ReceiveCloseAndAcknowledgeAsync(
            CancellationToken cancellationToken) =>
            await BrowserRefreshBridgeTests.ReceiveCloseAndAcknowledgeAsync(
                _socket,
                cancellationToken);

        internal async Task SendTextFragmentsAsync(
            IReadOnlyList<string> fragments,
            CancellationToken cancellationToken) =>
            await BrowserRefreshBridgeTests.SendTextFragmentsAsync(
                _socket,
                fragments,
                cancellationToken);

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                if (_socket.State is WebSocketState.Open
                    or WebSocketState.CloseReceived)
                {
                    using CancellationTokenSource cancellationSource = new(
                        TimeSpan.FromSeconds(1));
                    await _socket.CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Test connection stopped.",
                        cancellationSource.Token);
                }
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
                _socket.Dispose();
                _completion.TrySetResult();
            }
        }
    }
}
