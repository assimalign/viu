using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

namespace Assimalign.Viu.ServerRenderer.Tests;

public sealed class ServerRenderAdaptorTests
{
    [Fact]
    public async Task RenderAsync_GeneratedRegistry_UsesCompiledBodyWithoutClientTreeRender()
    {
        ComponentReference componentReference = ComponentReference.ForName("compiled-root");
        int setupCount = 0;
        int prefetchCount = 0;
        int clientRenderCount = 0;
        int compiledRenderCount = 0;
        int disposalCount = 0;
        ComponentFactory components = new();
        components.Register(new ComponentRegistration(
            componentReference,
            new ComponentContract(),
            _ => new InlineComponent(
                context =>
                {
                    setupCount++;
                    context.Lifecycle.OnServerPrefetch(() => prefetchCount++);
                    return _ =>
                    {
                        clientRenderCount++;
                        return new TextNode("client fallback");
                    };
                },
                () => disposalCount++)));
        ServerRenderRegistry serverRenders = new();
        serverRenders.Register(new ServerRenderRegistration(
            componentReference,
            (state, component, frame, scope) =>
            {
                component.ShouldBeOfType<InlineComponent>();
                frame.ShouldNotBeNull();
                scope.Context.ShouldNotBeNull();
                compiledRenderCount++;
                state.Push("<strong>compiled</strong>");
                return Task.CompletedTask;
            }));
        DelegateRequestScopeFactory<string> factory = new(
            (request, _) => CreateScope(request, components));
        FakeServerHost<string> host = new(factory, serverRenders);
        RecordingServerRenderOutput output = new();

        ServerRenderResult result = await host.RenderAsync(
            new ServerRenderRequest<string>(
                new ComponentNode(componentReference),
                "compiled"),
            output);

        result.Succeeded.ShouldBeTrue();
        output.Text.ShouldBe("<strong>compiled</strong>");
        setupCount.ShouldBe(1);
        prefetchCount.ShouldBe(1);
        compiledRenderCount.ShouldBe(1);
        clientRenderCount.ShouldBe(0);
        disposalCount.ShouldBe(1);
        factory.CreatedScopes.ShouldHaveSingleItem().DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task RenderAsync_ParallelRequests_FreshScopesKeepReactiveAndSsrContextStateIsolated()
    {
        ComponentReference componentReference = ComponentReference.ForName("request-root");
        ParallelArrival arrival = new(2);
        DelegateRequestScopeFactory<string> factory = new((request, _) =>
        {
            Reference<string> requestState = Reactive.Reference(request.RequestContext);
            ComponentFactory components = new();
            components.Register(Registration(
                componentReference,
                context =>
                {
                    context.Lifecycle.OnServerPrefetch(arrival.ArriveAsync);
                    return _ => new FragmentNode(
                    [
                        new TextNode(requestState.Value),
                        new TeleportNode("#request-state", [new TextNode(requestState.Value)]),
                    ]);
                }));
            return CreateScope(request, components);
        });
        FakeServerHost<string> host = new(factory);
        RecordingServerRenderOutput firstOutput = new();
        RecordingServerRenderOutput secondOutput = new();
        ServerRenderRequest<string> firstRequest = new(
            new ComponentNode(componentReference),
            "first-request");
        ServerRenderRequest<string> secondRequest = new(
            new ComponentNode(componentReference),
            "second-request");

        Task<ServerRenderResult> firstRendering = host.RenderAsync(
            firstRequest,
            firstOutput).AsTask();
        Task<ServerRenderResult> secondRendering = host.RenderAsync(
            secondRequest,
            secondOutput).AsTask();
        ServerRenderResult[] results = await Task.WhenAll(firstRendering, secondRendering);

        results.ShouldAllBe(result => result.Succeeded);
        firstOutput.Text.ShouldContain("first-request");
        firstOutput.Text.ShouldNotContain("second-request");
        secondOutput.Text.ShouldContain("second-request");
        secondOutput.Text.ShouldNotContain("first-request");
        results[0].Context.ShouldNotBeSameAs(results[1].Context);
        results[0].Context!.Teleports["#request-state"].ShouldContain("first-request");
        results[1].Context!.Teleports["#request-state"].ShouldContain("second-request");
        factory.CreatedScopes.Count.ShouldBe(2);
        factory.CreatedScopes.ShouldAllBe(scope => scope.DisposeCount == 1);
    }

    [Fact]
    public async Task RenderAsync_OverlappingStoreSetup_IsolatesAmbientScopeRegistryAndCleanup()
    {
        ComponentReference componentReference = ComponentReference.ForName("request-store-root");
        TaskCompletionSource firstSetupEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondSetupEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource firstSetupObserved = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ConcurrentDictionary<string, IReactiveEffectScope> observedScopes = new();
        ConcurrentDictionary<string, IStateStoreRegistry> expectedRegistries = new();
        ConcurrentDictionary<string, IStateStoreRegistry> observedRegistries = new();
        ConcurrentDictionary<string, int> cleanupCounts = new();
        DelegateRequestScopeFactory<string> factory = new((request, _) =>
        {
            string requestName = request.RequestContext;
            IStateStoreRegistry registry = StateStores.CreateRegistry();
            expectedRegistries[requestName] = registry;
            StateStoreDefinition<RequestSetupStore> definition = StateStores.Define(
                "request-store",
                context =>
                {
                    if (string.Equals(requestName, "first-request", StringComparison.Ordinal))
                    {
                        firstSetupEntered.TrySetResult();
                        if (!secondSetupEntered.Task.Wait(TimeSpan.FromSeconds(5)))
                        {
                            throw new TimeoutException("The second request did not enter store setup.");
                        }
                    }
                    else
                    {
                        secondSetupEntered.TrySetResult();
                        if (!firstSetupObserved.Task.Wait(TimeSpan.FromSeconds(5)))
                        {
                            throw new TimeoutException("The first request did not observe its setup state.");
                        }
                    }

                    IReactiveEffectScope currentScope = Reactive.CurrentScope
                        ?? throw new InvalidOperationException(
                            "Store setup did not run inside its registry-owned reactive scope.");
                    if (!ReferenceEquals(currentScope, context.Scope))
                    {
                        throw new InvalidOperationException(
                            "Store setup observed another request's reactive scope.");
                    }

                    observedScopes[requestName] = currentScope;
                    observedRegistries[requestName] = StateStores.ActiveRegistry
                        ?? throw new InvalidOperationException(
                            "Store setup lost its request's ambient registry.");
                    Reactive.OnScopeDispose(
                        () => cleanupCounts.AddOrUpdate(requestName, 1, static (_, count) => count + 1));
                    if (string.Equals(requestName, "first-request", StringComparison.Ordinal))
                    {
                        firstSetupObserved.TrySetResult();
                    }

                    return new RequestSetupStore(requestName);
                });
            ComponentFactory components = new();
            components.Register(Registration(
                componentReference,
                _ =>
                {
                    StateStores.SetActiveRegistry(registry);
                    RequestSetupStore store = definition.Use();
                    return _ => new TextNode(store.RequestName);
                }));
            return CreateScope(
                request,
                components,
                () =>
                {
                    registry.Dispose();
                    return ValueTask.CompletedTask;
                });
        });
        FakeServerHost<string> host = new(factory);
        ServerRenderRequest<string> firstRequest = new(
            new ComponentNode(componentReference),
            "first-request");
        ServerRenderRequest<string> secondRequest = new(
            new ComponentNode(componentReference),
            "second-request");
        RecordingServerRenderOutput firstOutput = new();
        RecordingServerRenderOutput secondOutput = new();

        Task<ServerRenderResult> firstRendering = Task.Run(
            () => host.RenderAsync(firstRequest, firstOutput).AsTask());
        await firstSetupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<ServerRenderResult> secondRendering = Task.Run(
            () => host.RenderAsync(secondRequest, secondOutput).AsTask());
        ServerRenderResult[] results = await Task.WhenAll(firstRendering, secondRendering)
            .WaitAsync(TimeSpan.FromSeconds(10));

        results.ShouldAllBe(result => result.Succeeded);
        firstOutput.Text.ShouldBe("first-request");
        secondOutput.Text.ShouldBe("second-request");
        observedScopes["first-request"].ShouldNotBeSameAs(observedScopes["second-request"]);
        observedRegistries["first-request"].ShouldBeSameAs(expectedRegistries["first-request"]);
        observedRegistries["second-request"].ShouldBeSameAs(expectedRegistries["second-request"]);
        cleanupCounts["first-request"].ShouldBe(1);
        cleanupCounts["second-request"].ShouldBe(1);
        factory.CreatedScopes.ShouldAllBe(scope => scope.DisposeCount == 1);
    }

    [Fact]
    public async Task RenderAsync_ReusedRequestScope_ReturnsFailureBeforeSecondResponseStarts()
    {
        ServerRenderRequest<string> request = new(new TextNode("fresh-once"), "request");
        TestRequestScope reusedScope = CreateScope(request, new ComponentFactory());
        DelegateRequestScopeFactory<string> factory = new((_, _) => reusedScope);
        FakeServerHost<string> host = new(factory);
        RecordingServerRenderOutput firstOutput = new();
        RecordingServerRenderOutput secondOutput = new();

        ServerRenderResult first = await host.RenderAsync(request, firstOutput);
        ServerRenderResult second = await host.RenderAsync(request, secondOutput);

        first.Succeeded.ShouldBeTrue();
        firstOutput.Text.ShouldBe("fresh-once");
        second.Succeeded.ShouldBeFalse();
        second.ResponseCommitted.ShouldBeFalse();
        second.Failure.ShouldBeOfType<InvalidOperationException>();
        second.Failure!.Message.ShouldContain("cannot be reused");
        secondOutput.Text.ShouldBeEmpty();
        reusedScope.DisposeCount.ShouldBe(2);
    }

    [Fact]
    public async Task RenderAsync_ReusedSsrContextWithFreshApplication_ReturnsIsolationFailure()
    {
        ServerRenderRequest<string> request = new(new TextNode("context"), "request");
        SsrContext reusedContext = new();
        DelegateRequestScopeFactory<string> factory = new((createdRequest, _) =>
            new TestRequestScope(
                new ServerRenderApplication(
                    createdRequest.RootComponent,
                    new ComponentFactory()),
                reusedContext,
                dispose: null));
        FakeServerHost<string> host = new(factory);

        ServerRenderResult first = await host.RenderAsync(
            request,
            new RecordingServerRenderOutput());
        ServerRenderResult second = await host.RenderAsync(
            request,
            new RecordingServerRenderOutput());

        first.Succeeded.ShouldBeTrue();
        second.Succeeded.ShouldBeFalse();
        second.ResponseCommitted.ShouldBeFalse();
        second.Failure.ShouldBeOfType<InvalidOperationException>();
        second.Failure!.Message.ShouldContain("SsrContext");
        factory.CreatedScopes.Count.ShouldBe(2);
        factory.CreatedScopes.ShouldAllBe(scope => scope.DisposeCount == 1);
    }

    [Fact]
    public async Task RenderAsync_MismatchedRoot_ConsumesBothApplicationAndContextIdentities()
    {
        TextNode requestedRoot = new("requested");
        TextNode ownedRoot = new("owned");
        ServerRenderApplication firstApplication = new(ownedRoot, new ComponentFactory());
        SsrContext firstContext = new();
        TestRequestScope firstScope = new(firstApplication, firstContext, dispose: null);
        TestRequestScope reusedApplicationScope = new(
            firstApplication,
            new SsrContext(),
            dispose: null);
        TestRequestScope reusedContextScope = new(
            new ServerRenderApplication(ownedRoot, new ComponentFactory()),
            firstContext,
            dispose: null);
        ConcurrentQueue<TestRequestScope> scopes = new(
            [firstScope, reusedApplicationScope, reusedContextScope]);
        DelegateRequestScopeFactory<string> factory = new((_, _) =>
            scopes.TryDequeue(out TestRequestScope? scope)
                ? scope
                : throw new InvalidOperationException("No request scope remained."));
        FakeServerHost<string> host = new(factory);

        ServerRenderResult mismatch = await host.RenderAsync(
            new ServerRenderRequest<string>(requestedRoot, "mismatch"),
            new RecordingServerRenderOutput());
        ServerRenderResult applicationReuse = await host.RenderAsync(
            new ServerRenderRequest<string>(ownedRoot, "application-reuse"),
            new RecordingServerRenderOutput());
        ServerRenderResult contextReuse = await host.RenderAsync(
            new ServerRenderRequest<string>(ownedRoot, "context-reuse"),
            new RecordingServerRenderOutput());

        mismatch.Succeeded.ShouldBeFalse();
        mismatch.Failure!.Message.ShouldContain("root component");
        applicationReuse.Succeeded.ShouldBeFalse();
        applicationReuse.Failure!.Message.ShouldContain("application");
        contextReuse.Succeeded.ShouldBeFalse();
        contextReuse.Failure!.Message.ShouldContain("SsrContext");
        firstScope.DisposeCount.ShouldBe(1);
        reusedApplicationScope.DisposeCount.ShouldBe(1);
        reusedContextScope.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task RenderAsync_FirstComponentBoundary_WritesBeforeCompletionAndAwaitsBackpressure()
    {
        ComponentReference rootReference = ComponentReference.ForName("stream-root");
        ComponentReference firstReference = ComponentReference.ForName("stream-first");
        ComponentReference secondReference = ComponentReference.ForName("stream-second");
        ComponentFactory components = new();
        components.Register(Registration(
            rootReference,
            _ => _ => Element(
                "main",
                [
                    new ComponentNode(firstReference),
                    new ComponentNode(secondReference),
                ])));
        components.Register(Registration(
            firstReference,
            _ => _ => Element("span", [new TextNode("first")])));
        components.Register(Registration(
            secondReference,
            _ => _ => Element("span", [new TextNode("second")])));
        DelegateRequestScopeFactory<string> factory = new(
            (request, _) => CreateScope(request, components));
        FakeServerHost<string> host = new(factory);
        TaskCompletionSource releaseFirstFlush = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingServerRenderOutput output = new(releaseFirstFlush);
        ServerRenderRequest<string> request = new(
            new ComponentNode(rootReference),
            "streaming");

        Task<ServerRenderResult> rendering = host.RenderAsync(request, output).AsTask();
        await output.FirstWrite.Task.WaitAsync(TimeSpan.FromSeconds(5));

        output.Text.ShouldBe("<main><span>first</span>");
        output.FlushCount.ShouldBe(1);
        rendering.IsCompleted.ShouldBeFalse();

        releaseFirstFlush.SetResult();
        ServerRenderResult result = await rendering.WaitAsync(TimeSpan.FromSeconds(5));

        result.Succeeded.ShouldBeTrue();
        output.Text.ShouldBe("<main><span>first</span><span>second</span></main>");
        output.FlushCount.ShouldBe(3);
        factory.CreatedScopes.ShouldHaveSingleItem().DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task RenderAsync_RequestCancellation_PropagatesAndDisposesEveryLease()
    {
        ComponentReference componentReference = ComponentReference.ForName("cancel-request");
        TaskCompletionSource prefetchStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource componentDisposed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ComponentFactory components = new();
        components.Register(new ComponentRegistration(
            componentReference,
            new ComponentContract(),
            _ => new InlineComponent(
                context =>
                {
                    context.Lifecycle.OnServerPrefetch(async cancellationToken =>
                    {
                        prefetchStarted.SetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    });
                    return _ => new TextNode("never");
                },
                () => componentDisposed.TrySetResult())));
        DelegateRequestScopeFactory<string> factory = new(
            (request, _) => CreateScope(request, components));
        FakeServerHost<string> host = new(factory);
        RecordingServerRenderOutput output = new();
        using CancellationTokenSource cancellationSource = new();
        ServerRenderRequest<string> request = new(
            new ComponentNode(componentReference),
            "cancel");

        Task<ServerRenderResult> rendering = host.RenderAsync(
            request,
            output,
            cancellationSource.Token).AsTask();
        await prefetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await rendering.WaitAsync(TimeSpan.FromSeconds(5)));
        await componentDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // [SSR-13] flows the exact request-abort token through request-scope teardown.
        TestRequestScope scope = factory.CreatedScopes.ShouldHaveSingleItem();
        scope.DisposeCount.ShouldBe(1);
        scope.DisposalCancellationToken.ShouldBe(cancellationSource.Token);
        scope.DisposalCancellationToken.IsCancellationRequested.ShouldBeTrue();
        output.Text.ShouldBeEmpty();
    }

    [Fact]
    public async Task RenderAsync_RenderFailureAfterBufferedProgress_ReportsHostUncommittedState()
    {
        ComponentReference rootReference = ComponentReference.ForName("failure-root");
        ComponentReference firstReference = ComponentReference.ForName("failure-first");
        ComponentReference failingReference = ComponentReference.ForName("failure-second");
        ComponentFactory components = new();
        components.Register(Registration(
            rootReference,
            _ => _ => Element(
                "main",
                [
                    new ComponentNode(firstReference),
                    new ComponentNode(failingReference),
                ])));
        components.Register(Registration(
            firstReference,
            _ => _ => new TextNode("progress")));
        components.Register(Registration(
            failingReference,
            _ => _ => throw new InvalidOperationException("render failure")));
        DelegateRequestScopeFactory<string> factory = new(
            (request, _) => CreateScope(request, components));
        FakeServerHost<string> host = new(factory);
        RecordingServerRenderOutput output = new(commitOnFlush: false);
        ServerRenderRequest<string> request = new(
            new ComponentNode(rootReference),
            "failure");

        ServerRenderResult result = await host.RenderAsync(request, output);

        result.Succeeded.ShouldBeFalse();
        // [SSR-12] trusts the buffered host's commitment state, not Viu's attempted writes.
        result.ResponseCommitted.ShouldBeFalse();
        result.Failure.ShouldBeOfType<InvalidOperationException>();
        result.Failure!.Message.ShouldBe("render failure");
        output.Text.ShouldBe("<main>progress");
        factory.CreatedScopes.ShouldHaveSingleItem().DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task RenderAsync_RequestScopeDisposalFailure_ReturnsFailureAfterContent()
    {
        ServerRenderRequest<string> request = new(new TextNode("content"), "dispose");
        DelegateRequestScopeFactory<string> factory = new((createdRequest, _) =>
            CreateScope(
                createdRequest,
                new ComponentFactory(),
                () => ValueTask.FromException(
                    new InvalidOperationException("scope disposal failure"))));
        FakeServerHost<string> host = new(factory);
        RecordingServerRenderOutput output = new();

        ServerRenderResult result = await host.RenderAsync(request, output);

        result.Succeeded.ShouldBeFalse();
        result.ResponseCommitted.ShouldBeTrue();
        result.Failure.ShouldBeOfType<InvalidOperationException>();
        result.Failure!.Message.ShouldBe("scope disposal failure");
        output.Text.ShouldBe("content");
        factory.CreatedScopes.ShouldHaveSingleItem().DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task RenderDocumentAsync_PrefixMainAndTeleportSuffix_StreamInOrder()
    {
        ComponentReference componentReference = ComponentReference.ForName("document-root");
        TaskCompletionSource prefetchStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releasePrefetch = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ComponentFactory components = new();
        components.Register(Registration(
            componentReference,
            context =>
            {
                context.Lifecycle.OnServerPrefetch(async cancellationToken =>
                {
                    prefetchStarted.TrySetResult();
                    await releasePrefetch.Task.WaitAsync(cancellationToken);
                });
                return _ => new FragmentNode(
                [
                    new TextNode("main-content"),
                    new TeleportNode("#modal", [new TextNode("teleported")]),
                ]);
            }));
        DelegateRequestScopeFactory<string> factory = new(
            (request, _) => CreateScope(request, components));
        FakeServerHost<string> host = new(factory);
        RecordingServerRenderOutput output = new();
        TeleportDocumentShell documentShell = new("#modal");
        ServerRenderRequest<string> request = new(
            new ComponentNode(componentReference),
            "document");

        Task<ServerRenderResult> rendering = host.RenderDocumentAsync(
            request,
            output,
            documentShell).AsTask();
        await prefetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // [SSR-14] flushes the shell prefix before the main component begins producing output.
        output.Text.ShouldBe("<!doctype html><html><body><main id=\"app\">");
        output.FlushCount.ShouldBe(1);
        documentShell.SuffixInvocationCount.ShouldBe(0);
        rendering.IsCompleted.ShouldBeFalse();

        releasePrefetch.TrySetResult();
        ServerRenderResult result = await rendering.WaitAsync(TimeSpan.FromSeconds(5));

        result.Succeeded.ShouldBeTrue(result.Failure?.ToString());
        result.ResponseCommitted.ShouldBeTrue();
        documentShell.SuffixInvocationCount.ShouldBe(1);
        // [SSR-14] exposes completed teleport payloads only to the post-render suffix.
        output.Text.ShouldBe(
            "<!doctype html><html><body><main id=\"app\">"
            + HydrationMarkers.FragmentStart
            + "main-content"
            + HydrationMarkers.TeleportStart
            + HydrationMarkers.TeleportEnd
            + HydrationMarkers.FragmentEnd
            + "</main><aside id=\"modal\">teleported"
            + HydrationMarkers.TeleportAnchor
            + "</aside></body></html>");
        factory.CreatedScopes.ShouldHaveSingleItem().DisposeCount.ShouldBe(1);
    }

    [Fact]
    public void SsrStateIsland_RawPayload_UsesSafeMarkupAndSourceGeneratedRestore()
    {
        const string hostileJson = "{\"value\":\"</script><div>\"}";

        string markup = SsrStateIsland.CreateMarkup(hostileJson);
        StateIslandTestPayload? payload = SsrStateIsland.Deserialize(
            "{\"message\":\"restored\"}",
            StateIslandTestJsonSerializerContext.Default.StateIslandTestPayload);

        markup.ShouldStartWith(
            "<script type=\"application/json\" data-viu-state>");
        markup.ShouldContain("\\u003C/script\\u003E\\u003Cdiv\\u003E");
        markup.ShouldEndWith("</script>");
        payload.ShouldNotBeNull();
        payload.Message.ShouldBe("restored");
    }

    [Fact]
    public async Task RenderAsync_TcpListenerHost_ServesMarkupWithoutAWebFramework()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        DelegateRequestScopeFactory<string> factory = new(
            (request, _) => CreateScope(request, new ComponentFactory()));
        FakeServerHost<string> host = new(factory);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        Task server = ServeSingleRequestAsync(listener, host, timeout.Token);
        using HttpClient client = new();

        string response;
        try
        {
            response = await client.GetStringAsync(
                new Uri($"http://127.0.0.1:{port}/smoke", UriKind.Absolute),
                timeout.Token);
            await server.WaitAsync(timeout.Token);
        }
        finally
        {
            listener.Stop();
        }

        response.ShouldBe("<main>http-smoke</main>");
        factory.CreatedScopes.ShouldHaveSingleItem().DisposeCount.ShouldBe(1);
    }

    private static async Task ServeSingleRequestAsync(
        TcpListener listener,
        FakeServerHost<string> host,
        CancellationToken cancellationToken)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using NetworkStream stream = client.GetStream();
        using StreamReader reader = new(
            stream,
            Encoding.ASCII,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        string requestLine = await reader.ReadLineAsync(cancellationToken)
            ?? throw new InvalidOperationException("The test HTTP request had no request line.");
        string? line;
        do
        {
            line = await reader.ReadLineAsync(cancellationToken);
        }
        while (!string.IsNullOrEmpty(line));

        using StreamWriter writer = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: true);
        await writer.WriteAsync(
            "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nConnection: close\r\n\r\n"
                .AsMemory(),
            cancellationToken);
        await writer.FlushAsync(cancellationToken);

        TextWriterServerRenderOutput output = new(writer);
        ServerRenderResult result = await host.RenderAsync(
            new ServerRenderRequest<string>(
                Element("main", [new TextNode("http-smoke")]),
                requestLine),
            output,
            cancellationToken);
        result.Succeeded.ShouldBeTrue(result.Failure?.ToString());
    }

    private static TestRequestScope CreateScope<TContext>(
        ServerRenderRequest<TContext> request,
        ComponentFactory components,
        Func<ValueTask>? dispose = null)
        where TContext : notnull =>
        new(
            new ServerRenderApplication(request.RootComponent, components),
            new SsrContext(),
            dispose);

    private static ComponentRegistration Registration(
        ComponentReference reference,
        Func<ComponentContext, ComponentRenderer> setup) =>
        new(
            reference,
            new ComponentContract(),
            _ => new InlineComponent(setup));

    private static ElementNode Element(
        string name,
        IReadOnlyList<VirtualNode>? children = null) =>
        new(new QualifiedName(name), children: children);

    private sealed class FakeServerHost<TContext>
        where TContext : notnull
    {
        private readonly ServerRenderAdaptor<TContext> _adaptor;

        internal FakeServerHost(IServerRenderRequestScopeFactory<TContext> requestScopeFactory)
        {
            _adaptor = new ServerRenderAdaptor<TContext>(requestScopeFactory);
        }

        internal FakeServerHost(
            IServerRenderRequestScopeFactory<TContext> requestScopeFactory,
            IServerRenderRegistry serverRenders)
        {
            _adaptor = new ServerRenderAdaptor<TContext>(requestScopeFactory, serverRenders);
        }

        internal ValueTask<ServerRenderResult> RenderAsync(
            ServerRenderRequest<TContext> request,
            IServerRenderOutput output,
            CancellationToken cancellationToken = default) =>
            _adaptor.RenderAsync(request, output, cancellationToken);

        internal ValueTask<ServerRenderResult> RenderDocumentAsync(
            ServerRenderRequest<TContext> request,
            IServerRenderOutput output,
            IServerRenderDocumentShell documentShell,
            CancellationToken cancellationToken = default) =>
            _adaptor.RenderDocumentAsync(request, output, documentShell, cancellationToken);
    }

    private sealed class DelegateRequestScopeFactory<TContext> :
        IServerRenderRequestScopeFactory<TContext>
        where TContext : notnull
    {
        private readonly Func<
            ServerRenderRequest<TContext>,
            CancellationToken,
            IServerRenderRequestScope> _create;

        internal DelegateRequestScopeFactory(
            Func<
                ServerRenderRequest<TContext>,
                CancellationToken,
                IServerRenderRequestScope> create)
        {
            _create = create;
        }

        internal ConcurrentBag<TestRequestScope> CreatedScopes { get; } = [];

        public ValueTask<IServerRenderRequestScope> CreateAsync(
            ServerRenderRequest<TContext> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IServerRenderRequestScope scope = _create(request, cancellationToken);
            CreatedScopes.Add((TestRequestScope)scope);
            return ValueTask.FromResult(scope);
        }
    }

    private sealed class TestRequestScope : IServerRenderRequestScope
    {
        private readonly Func<ValueTask>? _dispose;
        private CancellationToken _disposalCancellationToken;
        private int _disposeCount;

        internal TestRequestScope(
            ServerRenderApplication application,
            SsrContext renderContext,
            Func<ValueTask>? dispose)
        {
            Application = application;
            RenderContext = renderContext;
            _dispose = dispose;
        }

        internal int DisposeCount => Volatile.Read(ref _disposeCount);

        internal CancellationToken DisposalCancellationToken => _disposalCancellationToken;

        public ServerRenderApplication Application { get; }

        public SsrContext RenderContext { get; }

        public ValueTask DisposeAsync() => DisposeAsync(CancellationToken.None);

        public async ValueTask DisposeAsync(CancellationToken cancellationToken)
        {
            _disposalCancellationToken = cancellationToken;
            Interlocked.Increment(ref _disposeCount);
            if (_dispose is not null)
            {
                await _dispose().ConfigureAwait(false);
            }
        }
    }

    private sealed class RecordingServerRenderOutput : IServerRenderOutput
    {
        private readonly object _synchronization = new();
        private readonly StringBuilder _text = new();
        private readonly TaskCompletionSource? _releaseFirstFlush;
        private readonly bool _commitOnFlush;
        private int _flushCount;
        private int _responseCommitted;

        internal RecordingServerRenderOutput(
            TaskCompletionSource? releaseFirstFlush = null,
            bool commitOnFlush = true)
        {
            _releaseFirstFlush = releaseFirstFlush;
            _commitOnFlush = commitOnFlush;
        }

        internal TaskCompletionSource FirstWrite { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal int FlushCount => Volatile.Read(ref _flushCount);

        public bool ResponseCommitted => Volatile.Read(ref _responseCommitted) == 1;

        internal string Text
        {
            get
            {
                lock (_synchronization)
                {
                    return _text.ToString();
                }
            }
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<char> content,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_synchronization)
            {
                _text.Append(content.Span);
            }

            FirstWrite.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            int count = Interlocked.Increment(ref _flushCount);
            if (count == 1 && _releaseFirstFlush is not null)
            {
                await _releaseFirstFlush.Task.WaitAsync(cancellationToken);
            }

            if (_commitOnFlush)
            {
                Volatile.Write(ref _responseCommitted, 1);
            }
        }
    }

    private sealed class TextWriterServerRenderOutput : IServerRenderOutput
    {
        private readonly TextWriter _writer;
        private bool _responseCommitted;

        internal TextWriterServerRenderOutput(TextWriter writer)
        {
            _writer = writer;
        }

        public bool ResponseCommitted => _responseCommitted;

        public ValueTask WriteAsync(
            ReadOnlyMemory<char> content,
            CancellationToken cancellationToken = default) =>
            new(_writer.WriteAsync(content, cancellationToken));

        public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            _responseCommitted = true;
        }
    }

    private sealed class TeleportDocumentShell : IServerRenderDocumentShell
    {
        private readonly string _target;

        internal TeleportDocumentShell(string target)
        {
            _target = target;
        }

        internal int SuffixInvocationCount { get; private set; }

        public ValueTask WritePrefixAsync(
            IServerRenderOutput output,
            CancellationToken cancellationToken = default) =>
            output.WriteAsync(
                "<!doctype html><html><body><main id=\"app\">".AsMemory(),
                cancellationToken);

        public async ValueTask WriteSuffixAsync(
            IServerRenderOutput output,
            IReadOnlyDictionary<string, string> teleports,
            CancellationToken cancellationToken = default)
        {
            SuffixInvocationCount++;
            await output.WriteAsync(
                "</main><aside id=\"modal\">".AsMemory(),
                cancellationToken);
            if (teleports.TryGetValue(_target, out string? content))
            {
                await output.WriteAsync(content.AsMemory(), cancellationToken);
            }

            await output.WriteAsync(
                "</aside></body></html>".AsMemory(),
                cancellationToken);
        }
    }

    private sealed class ParallelArrival
    {
        private readonly int _requiredCount;
        private readonly TaskCompletionSource _arrived = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivalCount;

        internal ParallelArrival(int requiredCount)
        {
            _requiredCount = requiredCount;
        }

        internal async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivalCount) == _requiredCount)
            {
                _arrived.TrySetResult();
            }

            await _arrived.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class InlineComponent : IComponent, IDisposable
    {
        private readonly Func<ComponentContext, ComponentRenderer> _setup;
        private readonly Action? _dispose;

        internal InlineComponent(
            Func<ComponentContext, ComponentRenderer> setup,
            Action? dispose = null)
        {
            _setup = setup;
            _dispose = dispose;
        }

        public ComponentRenderer Setup(ComponentContext context) => _setup(context);

        public void Dispose() => _dispose?.Invoke();
    }

    private sealed class RequestSetupStore
    {
        internal RequestSetupStore(string requestName)
        {
            RequestName = requestName;
        }

        internal string RequestName { get; }
    }
}

internal sealed class StateIslandTestPayload
{
    public string Message { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(StateIslandTestPayload))]
internal sealed partial class StateIslandTestJsonSerializerContext : JsonSerializerContext;
