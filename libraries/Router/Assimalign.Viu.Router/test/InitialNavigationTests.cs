using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Pins the initial-navigation semantics ([V01.01.08.07], issue #219): CurrentRoute starts at the
// RouteLocation.Start sentinel, the first navigation runs the full guard pipeline with `from` set to
// that sentinel, the confirm step replaces rather than pushes the current history entry, and
// ReadyAsync always settles, and caller cancellation reaches the initial guard pipeline. Run counts
// are pinned so the initial pass fires each guard exactly once with no double resolution. All
// DOM-free through memory history (the RouterView case adds the in-memory Testing renderer).
public class InitialNavigationTests
{
    private static IReadOnlyList<RouteRecord> Routes() =>
    [
        new RouteRecord("/", name: "home"),
        new RouteRecord("/top", name: "top"),
        new RouteRecord("/a", name: "a"),
    ];

    [Fact]
    public void CurrentRoute_BeforeAnyNavigation_IsTheStartSentinel()
    {
        // CurrentRoute initializes to the Start sentinel (path "/", empty matched),
        // never the eagerly resolved initial location.
        var router = new Router(RouterHistory.CreateMemory(), Routes());

        var current = router.CurrentRoute.Value;

        current.ShouldBeSameAs(RouteLocation.Start);
        current.Path.ShouldBe("/");
        current.IsMatched.ShouldBeFalse();
        current.Matched.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadyAsync_RunsTheFullGuardPipelineFromStart_EachGuardExactlyOnce()
    {
        // The initial navigation runs the whole pipeline with from = START: the leave phase is
        // trivially empty (START has no matched records), and beforeEach / beforeEnter / beforeRouteEnter
        // / beforeResolve / afterEach each fire exactly once.
        var log = new List<string>();
        RouteLocation? beforeEachFrom = null;
        RouteLocation? afterEachFrom = null;
        NavigationFailure? afterEachFailure = null;
        var enterGuard = new EnterGuardComponent(log);
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord(
                    "/",
                    name: "home",
                    beforeEnter: (_, _, _) =>
                    {
                        log.Add("beforeEnter");
                        return Task.FromResult(NavigationGuardResult.Allow);
                    },
                    routeEnterGuard: enterGuard),
            ]);
        router.BeforeEach((_, from, _) =>
        {
            log.Add("beforeEach");
            beforeEachFrom = from;
            return Task.FromResult(NavigationGuardResult.Allow);
        });
        router.BeforeResolve((_, _, _) =>
        {
            log.Add("beforeResolve");
            return Task.FromResult(NavigationGuardResult.Allow);
        });
        router.AfterEach((_, from, failure) =>
        {
            log.Add("afterEach");
            afterEachFrom = from;
            afterEachFailure = failure;
        });

        var failure = await router.ReadyAsync();

        failure.ShouldBeNull();
        log.ShouldBe(["beforeEach", "beforeEnter", "beforeRouteEnter", "beforeResolve", "afterEach"]);
        beforeEachFrom.ShouldBeSameAs(RouteLocation.Start); // from === START for the initial navigation
        afterEachFrom.ShouldBeSameAs(RouteLocation.Start);
        afterEachFailure.ShouldBeNull();
        router.CurrentRoute.Value.Name.ShouldBe("home");
        router.CurrentRoute.Value.IsMatched.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadyAsync_RunsTheInitialBeforeEachRedirect()
    {
        // The exact #219 scenario: a global beforeEach redirect for the app entry URL. The router
        // starts at START, so the first resolve of "/" is not deduplicated and the redirect fires,
        // re-entering the pipeline once for "/top".
        var visited = new List<string>();
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        router.BeforeEach((to, _, _) =>
        {
            visited.Add(to.Path);
            return Task.FromResult(to.Path == "/"
                ? NavigationGuardResult.RedirectTo("/top")
                : NavigationGuardResult.Allow);
        });

        var failure = await router.ReadyAsync();

        failure.ShouldBeNull();
        visited.ShouldBe(["/", "/top"]); // resolved "/" then redirected to "/top" exactly once
        router.CurrentRoute.Value.Path.ShouldBe("/top");
        router.CurrentRoute.Value.Name.ShouldBe("top");
    }

    [Fact]
    public async Task ReadyAsync_ConfirmReplacesTheInitialHistoryEntry_RatherThanPushing()
    {
        // The first navigation forces a replace, so the application's entry URL is not
        // left as a stale back-target. In memory history a replace preserves the position counter (a
        // push would advance it to 1), and the redirected initial navigation still writes only once.
        var history = RouterHistory.CreateMemory();
        var router = new Router(history, Routes());
        router.BeforeEach((to, _, _) => Task.FromResult(
            to.Path == "/" ? NavigationGuardResult.RedirectTo("/top") : NavigationGuardResult.Allow));

        await router.ReadyAsync();

        history.Location.ShouldBe("/top");
        history.State.Position.ShouldBe(0); // replaced in place, not pushed
    }

    [Fact]
    public async Task ReadyAsync_IsIdempotent_RunningTheInitialNavigationOnce()
    {
        // Every call returns the same task (the initial navigation happens once; ReadyAsync resolves
        // to the settled result), so the guard runs exactly once no matter how many callers await.
        var beforeEachRuns = 0;
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        router.BeforeEach((_, _, _) =>
        {
            beforeEachRuns++;
            return Task.FromResult(NavigationGuardResult.Allow);
        });

        var first = router.ReadyAsync();
        var second = router.ReadyAsync();
        await Task.WhenAll(first, second);

        first.ShouldBeSameAs(second);
        beforeEachRuns.ShouldBe(1);
        router.CurrentRoute.Value.Name.ShouldBe("home");
    }

    [Fact]
    public async Task ReadyAsync_CancelledByCaller_CancelsTheInitialGuardPipeline()
    {
        TaskCompletionSource guardStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observedToken = default;
        using CancellationTokenSource cancellation = new();
        using Router router = new(
            RouterHistory.CreateMemory(),
            Routes());
        router.BeforeEach(
            async (_, _, token) =>
            {
                observedToken = token;
                guardStarted.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return NavigationGuardResult.Allow;
            });

        Task<NavigationFailure?> readiness =
            router.ReadyAsync(cancellation.Token);
        await guardStarted.Task;
        cancellation.Cancel();
        NavigationFailure? failure = await readiness;

        failure.ShouldNotBeNull();
        failure.Type.ShouldBe(NavigationFailureType.Cancelled);
        observedToken.IsCancellationRequested.ShouldBeTrue();
        router.CurrentRoute.Value.ShouldBeSameAs(RouteLocation.Start);
    }

    [Fact]
    public async Task ReadyAsync_WhenTheInitialGuardAborts_LeavesStartAndHistoryUntouched()
    {
        // An aborted initial navigation reports the failure and leaves CurrentRoute at START with the
        // history untouched. Because the initial pass runs through the application push path (never the
        // popstate listener), no compensating history.go fires for the initial resolution.
        var history = RouterHistory.CreateMemory();
        var router = new Router(history, Routes());
        router.BeforeEach((_, _, _) => Task.FromResult(NavigationGuardResult.Abort));

        var failure = await router.ReadyAsync();

        failure.ShouldNotBeNull();
        failure.Type.ShouldBe(NavigationFailureType.Aborted);
        router.CurrentRoute.Value.ShouldBeSameAs(RouteLocation.Start);
        history.Location.ShouldBe("/");
        history.State.Position.ShouldBe(0);
    }

    [Fact]
    public async Task AfterInitialNavigation_SameLocationPushIsStillDeduplicated()
    {
        // The START dedup skip is scoped to the initial pass only: once CurrentRoute has a matched
        // chain, an in-session push to the same location is a Duplicated no-op that never runs a guard.
        var beforeEachRuns = 0;
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        await router.ReadyAsync();
        await router.PushAsync("/a");
        router.BeforeEach((_, _, _) =>
        {
            beforeEachRuns++;
            return Task.FromResult(NavigationGuardResult.Allow);
        });

        var failure = await router.PushAsync("/a");

        failure.ShouldNotBeNull();
        failure.Type.ShouldBe(NavigationFailureType.Duplicated);
        beforeEachRuns.ShouldBe(0);
    }

    [Fact]
    public async Task RouterView_RendersNothingAtStart_ThenRendersAfterTheInitialNavigation()
    {
        // Nothing renders at the Start sentinel (empty matched). The outlet mounted before ReadyAsync shows
        // nothing, and the matched component renders exactly once after the initial navigation confirms.
        TrackingComponent view = LabelView("home");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/", component: view.Request)]);
        using var wrapper = MountView(router, view);

        wrapper.Find("div").ShouldBeNull();
        view.RenderCount.ShouldBe(0); // never rendered while the current route is START

        await router.ReadyAsync();
        await wrapper.NextTickAsync();

        wrapper.Html().ShouldBe("<div class=\"home\">home</div>");
        view.SetupCount.ShouldBe(1);
        view.RenderCount.ShouldBe(1); // rendered once — no double resolution of the initial navigation
    }

    // An explicitly registered component-associated enter guard. It is never activated as a
    // component, matching beforeRouteEnter's lack of an instance.
    private sealed class EnterGuardComponent : IRouteEnterGuard
    {
        private readonly List<string> _log;

        public EnterGuardComponent(List<string> log) => _log = log;

        public Task<NavigationGuardResult> BeforeRouteEnterAsync(RouteLocation to, RouteLocation from, CancellationToken cancellationToken)
        {
            _log.Add("beforeRouteEnter");
            return Task.FromResult(NavigationGuardResult.Allow);
        }
    }
}
