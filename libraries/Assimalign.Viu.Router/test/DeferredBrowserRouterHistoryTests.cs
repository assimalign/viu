using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins D5's lazy browser-history bootstrap: creating a web/hash history performs no interop,
// Router.ReadyAsync owns asynchronous initialization, listeners may be registered before readiness,
// and every other synchronous member fails with one actionable message until initialization settles.
public sealed class DeferredBrowserRouterHistoryTests
{
    [Fact]
    [SupportedOSPlatform("browser")]
    public void CreateWebAndHash_WithoutPrewarmingBridge_ReturnDeferredHistories()
    {
        IRouterHistory web = RouterHistory.CreateWeb();
        IRouterHistory hash = RouterHistory.CreateWebHash();

        web.ShouldBeOfType<DeferredBrowserRouterHistory>();
        hash.ShouldBeOfType<DeferredBrowserRouterHistory>();

        web.Destroy();
        hash.Destroy();
    }

    [Fact]
    public void SynchronousMembers_BeforeReady_ThrowTheSameActionableException()
    {
        (DeferredBrowserRouterHistory history, _) = CreateDeferred();
        Action[] operations =
        [
            () => _ = history.Base,
            () => _ = history.Location,
            () => _ = history.State,
            () => history.Push("/next"),
            () => history.Replace("/next"),
            () => history.Go(1),
            () => history.CreateHref("/next"),
        ];

        foreach (Action operation in operations)
        {
            InvalidOperationException exception =
                Should.Throw<InvalidOperationException>(operation);
            exception.Message.ShouldBe(
                DeferredBrowserRouterHistory.NotReadyMessage);
        }
    }

    [Fact]
    public async Task Listen_BeforeReady_ReceivesNavigationAfterInitialization()
    {
        (DeferredBrowserRouterHistory history, FakeBrowserHistoryInterop browserHistoryInterop) =
            CreateDeferred();
        List<(string To, string From, NavigationInformation Information)> observed = [];
        Action stopListening = history.Listen(
            (to, from, information) => observed.Add((to, from, information)));

        await history.InitializeAsync(CancellationToken.None);
        browserHistoryInterop.FirePopState(
            Snapshot(
                "/next",
                StateAt("/next", position: 1, back: "/")));

        (string to, string from, NavigationInformation information) =
            observed.ShouldHaveSingleItem();
        to.ShouldBe("/next");
        from.ShouldBe("/");
        information.Delta.ShouldBe(1);

        stopListening();
        stopListening();
        browserHistoryInterop.FirePopState(
            Snapshot(
                "/last",
                StateAt("/last", position: 2, back: "/next")));
        observed.Count.ShouldBe(1);

        history.Destroy();
    }

    [Fact]
    public async Task ReadyAsync_AwaitsLazyInitialization_AndRetainsFirstCallTaskAndToken()
    {
        TaskCompletionSource initialization =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observedToken = default;
        int initializationCount = 0;
        (DeferredBrowserRouterHistory history, FakeBrowserHistoryInterop browserHistoryInterop) =
            CreateDeferred(
                token =>
                {
                    initializationCount++;
                    observedToken = token;
                    return initialization.Task;
                });
        using CancellationTokenSource firstCancellation = new();
        using CancellationTokenSource ignoredCancellation = new();
        ignoredCancellation.Cancel();
        using Router router = new(
            history,
            [new RouteRecord("/", name: "home")]);

        Task<NavigationFailure?> first =
            router.ReadyAsync(firstCancellation.Token);
        Task<NavigationFailure?> second =
            router.ReadyAsync(ignoredCancellation.Token);

        second.ShouldBeSameAs(first);
        first.IsCompleted.ShouldBeFalse();
        initializationCount.ShouldBe(1);
        observedToken.ShouldBe(firstCancellation.Token);
        browserHistoryInterop.ReadSnapshotCount.ShouldBe(0);

        initialization.SetResult();
        NavigationFailure? failure = await first;

        failure.ShouldBeNull();
        router.CurrentRoute.Value.Name.ShouldBe("home");
        browserHistoryInterop.ReadSnapshotCount.ShouldBe(1);

        history.Destroy();
    }

    [Fact]
    public async Task ReadyAsync_CancellationDuringLazyInitialization_CancelsInitialization()
    {
        TaskCompletionSource initializationStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observedToken = default;
        (DeferredBrowserRouterHistory history, _) = CreateDeferred(
            async token =>
            {
                observedToken = token;
                initializationStarted.SetResult();
                await Task.Delay(Timeout.Infinite, token);
            });
        using CancellationTokenSource cancellation = new();
        using Router router = new(
            history,
            [new RouteRecord("/", name: "home")]);

        Task<NavigationFailure?> readiness =
            router.ReadyAsync(cancellation.Token);
        await initializationStarted.Task;
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => readiness);
        observedToken.ShouldBe(cancellation.Token);
        router.CurrentRoute.Value.ShouldBeSameAs(RouteLocation.Start);

        history.Destroy();
    }

    private static (
        DeferredBrowserRouterHistory History,
        FakeBrowserHistoryInterop BrowserHistoryInterop) CreateDeferred(
        Func<CancellationToken, Task>? initializeBridge = null)
    {
        FakeBrowserHistoryInterop browserHistoryInterop =
            new(Snapshot("/", StateAt("/", position: 0)));
        DeferredBrowserRouterHistory history = new(
            isHash: false,
            basePath: "/app",
            initializeBridge ?? (static _ => Task.CompletedTask),
            () => browserHistoryInterop);
        return (history, browserHistoryInterop);
    }

    private static BrowserHistorySnapshot Snapshot(
        string pathname,
        RouterHistoryState state)
        => new(
            pathname,
            string.Empty,
            string.Empty,
            "example.com",
            state.Position + 1,
            state);

    private static RouterHistoryState StateAt(
        string current,
        int position,
        string? back = null)
        => new(back, current, null, false, position, null);
}
