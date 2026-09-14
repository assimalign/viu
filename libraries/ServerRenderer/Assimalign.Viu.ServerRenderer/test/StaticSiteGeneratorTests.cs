using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;
using Assimalign.Viu.State;

using ViuRouter = Assimalign.Viu.Router.Router;

namespace Assimalign.Viu.ServerRenderer.Tests;

// [V01.01.07.05], [SSG-1] through [SSG-6]: complete documents use the ordinary SSR lifecycle.
public sealed class StaticSiteGeneratorTests
{
    private const string HostPage = "<!doctype html><html><head><base href=\"/docs/\"><script type=\"module\" src=\"main.abc.js\"></script></head><body><div id=\"app\">Loading</div></body></html>";

    [Theory]
    [InlineData("/", "index.html")]
    [InlineData("/?q=one#two", "index.html")]
    [InlineData("/guide/intro", "guide/intro/index.html")]
    [InlineData("/guide/intro/", "guide/intro/index.html")]
    [InlineData("/guide/intro?x=1#part?x=2", "guide/intro/index.html")]
    [InlineData("/caf%C3%A9", "café/index.html")]
    [InlineData("/a%20b", "a b/index.html")]
    public void GetRelativePath_Location_MapsPortableDocument(string route, string expected) =>
        StaticSitePath.GetRelativePath(route).ShouldBe(expected);

    [Theory]
    [InlineData("guide")]
    [InlineData("https://example.test/")]
    [InlineData("//server/path")]
    [InlineData("/a//b")]
    [InlineData("/a/../b")]
    [InlineData("/a/%2E%2E/b")]
    [InlineData("/a%2Fb")]
    [InlineData("/a%5Cb")]
    [InlineData("/a%00b")]
    [InlineData("/C:/file")]
    [InlineData("/NUL.txt")]
    [InlineData("/a.")]
    [InlineData("/a%20")]
    [InlineData("/bad%escape")]
    public void GetRelativePath_UnsafeLocation_RejectsInsteadOfEscapingOutput(string route) =>
        Should.Throw<ArgumentException>(() => StaticSitePath.GetRelativePath(route));

    [Fact]
    public async Task GenerateAsync_RoutedRequests_AwaitsGuardsAndParametersCapturesIsolatedStateAndDisposes()
    {
        List<RequestScope> scopes = [];
        List<IRouterHistory> histories = [];
        List<string> navigated = [];
        List<string> reported = [];
        ComponentRegistration page = ComponentRegistration.Define("static-page",
            new ComponentContract(parameters: [new ComponentParameter("id")]), context =>
            {
                string message = (string)context.Bindings.Parameters["id"]!;
                StateStoreDefinition<SsrPayloadStore> definition = StateStores.Define("route-state",
                    _ => new SsrPayloadStore { State = new SsrPayloadState { Message = message } },
                    new StateStoreJsonSerializer<SsrPayloadStore, SsrPayloadState>(
                        store => store.State, (store, state) => store.State = state,
                        ServerRendererStateJsonContext.Default.SsrPayloadState));
                SsrPayloadStore store = definition.Use(context);
                return _ => new FragmentNode([new ElementNode(new QualifiedName("main"),
                    children: [new TextNode(store.State.Message)])]);
            });
        Factory factory = new(request =>
        {
            ComponentFactory components = new();
            components.Register(page);
            components.Register(RouterView.Registration);
            ViuRouter router = new(request.RequestContext.History,
            [new RouteRecord("/items/:id", component: new ComponentNode(page.Reference),
                argumentsResolver: RouteComponentArguments.FromParameters())]);
            router.BeforeEach(async (destination, _, cancellationToken) =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                navigated.Add(destination.FullPath);
                return NavigationGuardResult.Allow;
            });
            IStateStoreRegistry state = StateStores.CreateRegistry();
            ServerRenderApplication application = ServerRenderApplication.CreateBuilder(
                request.RootComponent, components, new RouterServices(router))
                .ConfigureApplication(options => options.State = state).Build();
            RequestScope scope = new(application, () => { router.Dispose(); state.Dispose(); });
            scopes.Add(scope);
            return scope;
        });
        StaticSiteGenerator generator = new(() => new ComponentNode(RouterView.Registration.Reference), factory,
            basePath => { IRouterHistory history = RouterHistory.CreateMemory(basePath); histories.Add(history); return history; });
        MemoryOutput output = new();

        IReadOnlyList<StaticSiteFile> files = await generator.GenerateAsync(
            ["/items/first?q=value#part", "/items/second"], new HostPageDocumentShell(HostPage), output,
            "/docs", file => reported.Add(file.RelativePath));

        navigated.ShouldBe(["/items/first?q=value#part", "/items/second"]);
        files.Count.ShouldBe(2);
        reported.ShouldBe(["items/first/index.html", "items/second/index.html"]);
        output.Documents[reported[0]].ShouldContain(HydrationMarkers.FragmentStart + "<main>first</main>" + HydrationMarkers.FragmentEnd);
        output.Documents[reported[0]].ShouldContain("<script type=\"application/json\" data-viu-state>");
        output.Documents[reported[0]].ShouldContain("\"Message\":\"first\"");
        output.Documents[reported[0]].ShouldNotContain("second");
        output.Documents[reported[1]].ShouldContain("\"Message\":\"second\"");
        scopes[0].Application.ShouldNotBeSameAs(scopes[1].Application);
        scopes[0].RenderContext.ShouldNotBeSameAs(scopes[1].RenderContext);
        scopes.ShouldAllBe(scope => scope.DisposeCount == 1);
        histories.ForEach(history => Should.Throw<ObjectDisposedException>(() => history.Location));
    }

    [Fact]
    public async Task GenerateAsync_FailingSecondRoute_ReportsRouteKeepsEarlierFileAndStops()
    {
        List<RequestScope> scopes = [];
        Factory factory = new(request =>
        {
            if (request.RequestContext.Route == "/broken")
            {
                throw new InvalidOperationException("composition failed");
            }

            RequestScope scope = new(new ServerRenderApplication(request.RootComponent, new ComponentFactory()));
            scopes.Add(scope);
            return scope;
        });
        StaticSiteGenerator generator = new(() => new TextNode("complete"), factory);
        MemoryOutput output = new();
        List<string> reports = [];

        StaticSiteGenerationException failure = await Should.ThrowAsync<StaticSiteGenerationException>(() =>
            generator.GenerateAsync(["/first", "/broken", "/never"], new HostPageDocumentShell(HostPage), output,
                fileEmitted: file => reports.Add(file.Route)));

        failure.Route.ShouldBe("/broken");
        failure.Message.ShouldContain("composition failed");
        reports.ShouldBe(["/first"]);
        output.Documents.Count.ShouldBe(1);
        scopes.ShouldHaveSingleItem().DisposeCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("/a?x=1", "/a#two")]
    [InlineData("/a", "/a/")]
    [InlineData("/A", "/a")]
    [InlineData("/a", "/%61")]
    public async Task GenerateAsync_CollidingLocations_FailsInsteadOfOverwriting(string first, string second)
    {
        MemoryOutput output = new();
        StaticSiteGenerationException failure = await Should.ThrowAsync<StaticSiteGenerationException>(() =>
            SimpleGenerator().GenerateAsync([first, second], new HostPageDocumentShell(HostPage), output));
        failure.Route.ShouldBe(second);
        failure.Message.ShouldContain(first);
        output.Documents.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("render")]
    [InlineData("teardown")]
    [InlineData("output")]
    [InlineData("abort")]
    public async Task GenerateAsync_FailurePhase_DisposesScopeAndHistoryAndReportsNoSuccess(string phase)
    {
        IRouterHistory? history = null;
        RequestScope? scope = null;
        Factory factory = new(request =>
        {
            ViuRouter router = new(request.RequestContext.History, [new RouteRecord("/broken")]);
            router.BeforeEach((_, _, _) => Task.FromResult(phase == "abort" ? NavigationGuardResult.Abort : NavigationGuardResult.Allow));
            scope = new RequestScope(new ServerRenderApplication(request.RootComponent, new ComponentFactory(), new RouterServices(router)), () =>
            {
                router.Dispose();
                if (phase == "teardown")
                {
                    throw new InvalidOperationException("teardown failed");
                }
            });
            return scope;
        });
        StaticSiteGenerator generator = new(() => phase == "render"
            ? new ComponentNode(ComponentReference.ForName("unregistered")) : new TextNode("body"),
            factory, basePath => history = RouterHistory.CreateMemory(basePath));
        MemoryOutput output = new() { Fail = phase == "output" };
        int reports = 0;

        StaticSiteGenerationException failure = await Should.ThrowAsync<StaticSiteGenerationException>(() =>
            generator.GenerateAsync(["/broken"], new HostPageDocumentShell(HostPage), output, fileEmitted: _ => reports++));

        failure.Route.ShouldBe("/broken");
        scope.ShouldNotBeNull().DisposeCount.ShouldBe(1);
        history.ShouldNotBeNull();
        Should.Throw<ObjectDisposedException>(() => history.Location);
        output.Documents.ShouldBeEmpty();
        reports.ShouldBe(0);
    }

    [Fact]
    public async Task GenerateAsync_CancelledGuard_PropagatesCancellationAndCleansUp()
    {
        using CancellationTokenSource cancellation = new();
        RequestScope? scope = null;
        IRouterHistory? history = null;
        Factory factory = new(request =>
        {
            ViuRouter router = new(request.RequestContext.History, [new RouteRecord("/cancel")]);
            router.BeforeEach((_, _, _) => { cancellation.Cancel(); return Task.FromResult(NavigationGuardResult.Allow); });
            scope = new(new ServerRenderApplication(request.RootComponent, new ComponentFactory(), new RouterServices(router)), router.Dispose);
            return scope;
        });
        StaticSiteGenerator generator = new(() => new TextNode("body"), factory,
            basePath => history = RouterHistory.CreateMemory(basePath));
        MemoryOutput output = new();

        await Should.ThrowAsync<OperationCanceledException>(() => generator.GenerateAsync(["/cancel"],
            new HostPageDocumentShell(HostPage), output, cancellationToken: cancellation.Token));

        scope.ShouldNotBeNull().DisposeCount.ShouldBe(1);
        history.ShouldNotBeNull();
        Should.Throw<ObjectDisposedException>(() => history.Location);
        output.Documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_ReusedApplication_IsRejectedByRequestAdaptor()
    {
        TextNode root = new("same");
        ServerRenderApplication application = new(root, new ComponentFactory());
        StaticSiteGenerator generator = new(() => root, new Factory(_ => new RequestScope(application)));
        MemoryOutput output = new();
        StaticSiteGenerationException failure = await Should.ThrowAsync<StaticSiteGenerationException>(() =>
            generator.GenerateAsync(["/one", "/two"], new HostPageDocumentShell(HostPage), output));
        failure.Route.ShouldBe("/two");
        output.Documents.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GenerateAsync_ReadinessFailure_ConsumesApplicationAndContextBeforeDisposal(bool reuseApplication)
    {
        TextNode root = new("body");
        List<RequestScope> scopes = [];
        bool exposeRouter = true;
        Factory factory = new(request =>
        {
            RequestScope scope;
            if (scopes.Count == 0)
            {
                ViuRouter router = new(request.RequestContext.History, [new RouteRecord("/aborted")]);
                router.BeforeEach((_, _, _) => Task.FromResult(NavigationGuardResult.Abort));
                CallbackServices services = new(serviceType =>
                    exposeRouter && serviceType == typeof(ViuRouter) ? router : null);
                scope = new(new ServerRenderApplication(root, new ComponentFactory(), services), router.Dispose);
            }
            else
            {
                ServerRenderApplication application = reuseApplication
                    ? scopes[0].Application
                    : new ServerRenderApplication(root, new ComponentFactory());
                scope = new(application, renderContext: reuseApplication ? null : scopes[0].RenderContext);
            }

            scopes.Add(scope);
            return scope;
        });
        StaticSiteGenerator generator = new(() => root, factory);
        MemoryOutput output = new();
        await Should.ThrowAsync<StaticSiteGenerationException>(() =>
            generator.GenerateAsync(["/aborted"], new HostPageDocumentShell(HostPage), output));

        // [SSR-9], [SSG-1]: even a readiness-failed scope is consumed before it is disposed.
        // Removing its router ensures the second attempt reaches identity validation.
        exposeRouter = false;
        StaticSiteGenerationException failure = await Should.ThrowAsync<StaticSiteGenerationException>(() =>
            generator.GenerateAsync(["/again"], new HostPageDocumentShell(HostPage), output));

        failure.Route.ShouldBe("/again");
        failure.Message.ShouldContain("cannot be reused");
        scopes.Count.ShouldBe(2);
        scopes.ShouldAllBe(scope => scope.DisposeCount == 1);
        output.Documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_CancelledReadinessAndFailingTeardown_PreservesRequestCancellation()
    {
        using CancellationTokenSource cancellation = new();
        OperationCanceledException original = new(cancellation.Token);
        CallbackServices services = new(_ =>
        {
            cancellation.Cancel();
            throw original;
        });
        TextNode root = new("body");
        RequestScope scope = new(new ServerRenderApplication(root, new ComponentFactory(), services),
            () => throw new InvalidOperationException("cleanup failed"));
        StaticSiteRequestScopeFactory factory = new(new Factory(_ => scope));
        using IRouterHistory history = RouterHistory.CreateMemory();
        ServerRenderRequest<StaticSiteRouteContext> request = new(root,
            new StaticSiteRouteContext("/cancel", "/", history));

        OperationCanceledException failure = await Should.ThrowAsync<OperationCanceledException>(() =>
            factory.CreateAsync(request, cancellation.Token).AsTask());

        // [SSG-1], [SSR-13]: request cancellation remains authoritative after cleanup failure.
        failure.CancellationToken.ShouldBe(cancellation.Token);
        scope.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_ReadinessAndTeardownFailures_PreservesBothCauses()
    {
        InvalidOperationException readinessFailure = new("readiness failed");
        IOException teardownFailure = new("cleanup failed");
        CallbackServices services = new(_ => throw readinessFailure);
        TextNode root = new("body");
        RequestScope scope = new(new ServerRenderApplication(root, new ComponentFactory(), services),
            () => throw teardownFailure);
        StaticSiteRequestScopeFactory factory = new(new Factory(_ => scope));
        using IRouterHistory history = RouterHistory.CreateMemory();
        ServerRenderRequest<StaticSiteRouteContext> request = new(root,
            new StaticSiteRouteContext("/failure", "/", history));

        AggregateException failure = await Should.ThrowAsync<AggregateException>(() =>
            factory.CreateAsync(request).AsTask());

        // [SSG-5]: teardown failure cannot erase the original route failure.
        failure.InnerExceptions.ShouldBe(new Exception[] { readinessFailure, teardownFailure });
        scope.DisposeCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("navigation")]
    [InlineData("render")]
    public async Task GenerateAsync_RouteAndHistoryDisposalFailures_PreservesBothCauses(string phase)
    {
        InvalidOperationException original = new(phase + " failed");
        IOException cleanupFailure = new("history cleanup failed");
        ComponentRegistration registration = ComponentRegistration.Define("failing-static-root",
            new ComponentContract(), _ => _ => throw original);
        RequestScope? scope = null;
        DisposingHistory? history = null;
        Factory factory = new(request =>
        {
            ComponentFactory components = new();
            components.Register(registration);
            ViuRouter router = new(request.RequestContext.History, [new RouteRecord("/failure")]);
            router.BeforeEach((_, _, _) => phase == "navigation"
                ? Task.FromException<NavigationGuardResult>(original)
                : Task.FromResult(NavigationGuardResult.Allow));
            scope = new(new ServerRenderApplication(request.RootComponent, components,
                new RouterServices(router)), router.Dispose);
            return scope;
        });
        StaticSiteGenerator generator = new(() => new ComponentNode(registration.Reference), factory,
            basePath => history = new(RouterHistory.CreateMemory(basePath), () => throw cleanupFailure));
        MemoryOutput output = new();

        StaticSiteGenerationException failure = await Should.ThrowAsync<StaticSiteGenerationException>(() =>
            generator.GenerateAsync(["/failure"], new HostPageDocumentShell(HostPage), output));

        // [SSG-5]: a custom history's disposal cannot replace the original route failure.
        failure.Route.ShouldBe("/failure");
        AggregateException causes = failure.InnerException.ShouldBeOfType<AggregateException>();
        causes.InnerExceptions.ShouldBe(new Exception[] { original, cleanupFailure });
        scope.ShouldNotBeNull().DisposeCount.ShouldBe(1);
        history.ShouldNotBeNull().DisposeCount.ShouldBe(1);
        Should.Throw<ObjectDisposedException>(() => history.Location);
        output.Documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_CancellationAndHistoryDisposalFailure_PreservesCancellation()
    {
        using CancellationTokenSource cancellation = new();
        DisposingHistory? history = null;
        RequestScope? scope = null;
        Factory factory = new(request =>
        {
            ViuRouter router = new(request.RequestContext.History, [new RouteRecord("/cancel")]);
            router.BeforeEach((_, _, _) =>
            {
                cancellation.Cancel();
                return Task.FromResult(NavigationGuardResult.Allow);
            });
            scope = new(new ServerRenderApplication(request.RootComponent, new ComponentFactory(),
                new RouterServices(router)), router.Dispose);
            return scope;
        });
        StaticSiteGenerator generator = new(() => new TextNode("body"), factory,
            basePath => history = new(RouterHistory.CreateMemory(basePath),
                () => throw new IOException("history cleanup failed")));
        MemoryOutput output = new();

        OperationCanceledException failure = await Should.ThrowAsync<OperationCanceledException>(() =>
            generator.GenerateAsync(["/cancel"], new HostPageDocumentShell(HostPage), output,
                cancellationToken: cancellation.Token));

        failure.CancellationToken.ShouldBe(cancellation.Token);
        scope.ShouldNotBeNull().DisposeCount.ShouldBe(1);
        history.ShouldNotBeNull().DisposeCount.ShouldBe(1);
        Should.Throw<ObjectDisposedException>(() => history.Location);
        output.Documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task FileOutput_CompleteDocument_WritesUtf8AndRemovesStaleCompressedSidecars()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            FileSystemStaticSiteOutput output = new(directory);
            string destination = Path.Combine(directory, "guide", "index.html");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllTextAsync(destination + ".gz", "stale gzip");
            await File.WriteAllTextAsync(destination + ".br", "stale brotli");
            (await output.WriteAsync("guide/index.html", "café")).ShouldBe(destination);
            (await File.ReadAllBytesAsync(destination)).ShouldBe(new byte[] { 99, 97, 102, 195, 169 });
            File.Exists(destination + ".gz").ShouldBeFalse();
            File.Exists(destination + ".br").ShouldBeFalse();
            Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories).ShouldBeEmpty();
            await Should.ThrowAsync<ArgumentException>(() => output.WriteAsync("../escape.html", "bad").AsTask());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_RouteFileAndRepeatedRoute_ReadsHostBeforeRootOverwriteAndEmitsNestedDocument()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string host = Path.Combine(directory, "index.html");
            string routeFile = Path.Combine(directory, "routes.txt");
            await File.WriteAllTextAsync(host, HostPage);
            await File.WriteAllTextAsync(routeFile, "/\n\n/guide/intro?raw=1#part\n");
            int result = await StaticSiteGeneratorHost.RunAsync(
                ["prerender", "--host-page", host, "--output", directory, "--routes", routeFile,
                    "--route", "/third", "--base", "/docs"], SimpleGenerator);
            result.ShouldBe(0);
            foreach (string path in new[] { "index.html", "guide/intro/index.html", "third/index.html" })
            {
                string html = await File.ReadAllTextAsync(Path.Combine(directory, path));
                html.ShouldContain("<div id=\"app\">body</div>");
                html.ShouldContain("main.abc.js");
                html.ShouldContain("<base href=\"/docs/\">");
                html.ShouldNotContain("Loading");
            }

            (await StaticSiteGeneratorHost.RunAsync(["--unknown"], SimpleGenerator)).ShouldBe(1);
            (await StaticSiteGeneratorHost.RunAsync(["--output"], SimpleGenerator)).ShouldBe(1);
            (await StaticSiteGeneratorHost.RunAsync(["--host-page", host, "--output", directory, "--route", "/../invalid"], SimpleGenerator)).ShouldBe(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static StaticSiteGenerator SimpleGenerator() => new(() => new TextNode("body"),
        new Factory(request => new RequestScope(new ServerRenderApplication(request.RootComponent, new ComponentFactory()))));

    private static string CreateTemporaryDirectory()
    {
        // Keep test writes inside this worktree, under the existing test output directory.
        string directory = Path.Combine(AppContext.BaseDirectory, "static-site-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class Factory(Func<ServerRenderRequest<StaticSiteRouteContext>, IServerRenderRequestScope> create)
        : IServerRenderRequestScopeFactory<StaticSiteRouteContext>
    {
        public ValueTask<IServerRenderRequestScope> CreateAsync(ServerRenderRequest<StaticSiteRouteContext> request,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(create(request));
    }

    private sealed class RequestScope(
        ServerRenderApplication application,
        Action? dispose = null,
        SsrContext? renderContext = null) : IServerRenderRequestScope
    {
        internal int DisposeCount { get; private set; }
        public ServerRenderApplication Application { get; } = application;
        public SsrContext RenderContext { get; } = renderContext ?? new();
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            dispose?.Invoke();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RouterServices(ViuRouter router) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(ViuRouter) ? router : null;
    }

    private sealed class CallbackServices(Func<Type, object?> resolve) : IServiceProvider
    {
        public object? GetService(Type serviceType) => resolve(serviceType);
    }

    private sealed class DisposingHistory(IRouterHistory inner, Action afterDisposal) : IRouterHistory
    {
        internal int DisposeCount { get; private set; }

        public string Base => inner.Base;

        public string Location => inner.Location;

        public RouterHistoryState State => inner.State;

        public void Push(string location, RouterHistoryEntryOptions options = default) => inner.Push(location, options);

        public void Replace(string location, RouterHistoryEntryOptions options = default) => inner.Replace(location, options);

        public void Go(int delta, RouterHistoryNavigationOptions options = RouterHistoryNavigationOptions.None) =>
            inner.Go(delta, options);

        public Action Listen(NavigationCallback callback) => inner.Listen(callback);

        public string CreateHref(string location) => inner.CreateHref(location);

        public void Dispose()
        {
            DisposeCount++;
            inner.Dispose();
            afterDisposal();
        }
    }

    private sealed class MemoryOutput : IStaticSiteOutput
    {
        internal Dictionary<string, string> Documents { get; } = new(StringComparer.Ordinal);
        internal bool Fail { get; init; }
        public ValueTask<string> WriteAsync(string relativePath, string document, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Fail)
            {
                throw new IOException("storage failed");
            }
            Documents.Add(relativePath, document);
            return ValueTask.FromResult(relativePath);
        }
    }
}
