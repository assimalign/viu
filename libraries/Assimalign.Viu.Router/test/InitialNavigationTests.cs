using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Pins the initial-navigation / START-location semantics ([V01.01.08.07], issue #219) against
// vue-router (packages/router/src/router.ts: currentRoute starts at START_LOCATION_NORMALIZED, the
// first navigation runs the full pipeline with from === START and finalizeNavigation forces a
// replace; router.isReady resolves when it settles — https://router.vuejs.org/api/#isReady and
// #Variables-START-LOCATION). Run counts are pinned so the initial pass fires each guard exactly once
// with no double resolution. All DOM-free through memory history (the RouterView case adds the
// in-memory Testing renderer).
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
        // Upstream: currentRoute initializes to START_LOCATION_NORMALIZED (path "/", empty matched),
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
                    component: enterGuard,
                    beforeEnter: (_, _, _) =>
                    {
                        log.Add("beforeEnter");
                        return Task.FromResult(NavigationGuardResult.Allow);
                    }),
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
        // Upstream forces a replace when from === START (isFirstNavigation), so the app entry is not
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
        // Every call returns the same task (upstream: the initial push happens once; isReady resolves
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
        await router.Push("/a");
        router.BeforeEach((_, _, _) =>
        {
            beforeEachRuns++;
            return Task.FromResult(NavigationGuardResult.Allow);
        });

        var failure = await router.Push("/a");

        failure.ShouldNotBeNull();
        failure.Type.ShouldBe(NavigationFailureType.Duplicated);
        beforeEachRuns.ShouldBe(0);
    }

    [Fact]
    public async Task RouterView_RendersNothingAtStart_ThenRendersAfterTheInitialNavigation()
    {
        // Upstream renders nothing at START (empty matched). The outlet mounted before ReadyAsync shows
        // nothing, and the matched component renders exactly once after the initial navigation confirms.
        var view = LabelView("home");
        var router = new Router(RouterHistory.CreateMemory(), [new RouteRecord("/", component: view)]);
        using var wrapper = MountView(router);

        wrapper.Find("div").ShouldBeNull();
        view.RenderCount.ShouldBe(0); // never rendered while the current route is START

        await router.ReadyAsync();
        await wrapper.NextTickAsync();

        wrapper.Html().ShouldBe("<div class=\"home\">home</div>");
        view.SetupCount.ShouldBe(1);
        view.RenderCount.ShouldBe(1); // rendered once — no double resolution of the initial navigation
    }

    // A route component that contributes a beforeRouteEnter guard (interface-based, no reflection) and
    // logs when the pipeline invokes it. Never mounted here, so Setup is unused.
    private sealed class EnterGuardComponent : IComponent, IRouteEnterGuard
    {
        private readonly List<string> _log;

        public EnterGuardComponent(List<string> log) => _log = log;

        public string? Name => "enter";

        public Task<NavigationGuardResult> BeforeRouteEnter(RouteLocation to, RouteLocation from, CancellationToken cancellationToken)
        {
            _log.Add("beforeRouteEnter");
            return Task.FromResult(NavigationGuardResult.Allow);
        }

        public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
            => () => null;
    }
}
