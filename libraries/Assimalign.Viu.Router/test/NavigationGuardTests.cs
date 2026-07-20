using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins the guarded navigation pipeline ([V01.01.08.04]) against vue-router's navigation flow
// (packages/router/src/router.ts navigate()/pushWithRedirect, navigationGuards.ts, errors.ts;
// https://router.vuejs.org/guide/advanced/navigation-guards.html and
// https://router.vuejs.org/guide/advanced/navigation-failures.html): the awaitable push/replace
// result, allow/abort/duplicate/redirect outcomes, the redirect loop cap, per-route beforeEnter,
// removal handles, and onError routing. All DOM-free through memory history with no mounted view.
public class NavigationGuardTests
{
    private static IReadOnlyList<RouteRecord> Routes() =>
    [
        new RouteRecord("/", name: "home"),
        new RouteRecord("/a", name: "a"),
        new RouteRecord("/b", name: "b"),
    ];

    private static NavigationGuard Allow() => (_, _, _) => Task.FromResult(NavigationGuardResult.Allow);

    [Fact]
    public async Task Push_RunsGlobalBeforeEach_AndConfirmsOnAllow()
    {
        var log = new List<string>();
        var history = RouterHistory.CreateMemory();
        var router = new Router(history, Routes());
        router.BeforeEach((to, _, _) =>
        {
            log.Add(to.Path);
            return Task.FromResult(NavigationGuardResult.Allow);
        });

        var failure = await router.Push("/a");

        failure.ShouldBeNull();
        router.CurrentRoute.Value.Path.ShouldBe("/a");
        history.Location.ShouldBe("/a");
        log.ShouldBe(["/a"]);
    }

    [Fact]
    public async Task Push_WhenBeforeGuardAborts_LeavesRouteAndHistoryUntouched_AndReturnsAbortedFailure()
    {
        // vue-router: returning false aborts, currentRoute is untouched, and push resolves with the
        // failure rather than throwing.
        var history = RouterHistory.CreateMemory();
        var router = new Router(history, Routes());
        router.BeforeEach((_, _, _) => Task.FromResult(NavigationGuardResult.Abort));

        var failure = await router.Push("/a");

        failure.ShouldNotBeNull();
        failure.Type.ShouldBe(NavigationFailureType.Aborted);
        router.CurrentRoute.Value.Path.ShouldBe("/");
        history.Location.ShouldBe("/");
    }

    [Fact]
    public async Task Push_ToCurrentLocation_ReportsDuplicated_WithoutRunningTheGuardChain()
    {
        var beforeEachRuns = 0;
        var router = new Router(RouterHistory.CreateMemory(), Routes());
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
        router.CurrentRoute.Value.Path.ShouldBe("/a");
    }

    [Fact]
    public async Task AfterEach_ObservesBothSuccessAndFailure()
    {
        var observed = new List<(string Path, NavigationFailureType? Failure)>();
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        router.AfterEach((to, _, failure) => observed.Add((to.Path, failure?.Type)));

        await router.Push("/a");
        var removeAbort = router.BeforeEach((_, _, _) => Task.FromResult(NavigationGuardResult.Abort));
        await router.Push("/b");
        removeAbort();

        observed.ShouldBe([("/a", null), ("/b", NavigationFailureType.Aborted)]);
    }

    [Fact]
    public async Task Push_WhenGuardRedirects_RestartsThePipelineAgainstTheNewTarget()
    {
        var visited = new List<string>();
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/old"), new RouteRecord("/new")]);
        router.BeforeEach((to, _, _) =>
        {
            visited.Add(to.Path);
            return Task.FromResult(to.Path == "/old"
                ? NavigationGuardResult.RedirectTo("/new")
                : NavigationGuardResult.Allow);
        });

        var failure = await router.Push("/old");

        failure.ShouldBeNull();
        router.CurrentRoute.Value.Path.ShouldBe("/new");
        visited.ShouldBe(["/old", "/new"]);
    }

    [Fact]
    public async Task Push_WhenGuardRedirects_FiresAfterEachForTheFinalTargetOnly()
    {
        // Upstream recurses before triggerAfterEach, so the intermediate redirected navigation does
        // not surface an afterEach — only the confirmed final one does.
        var afterEachPaths = new List<string>();
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/old"), new RouteRecord("/new")]);
        router.AfterEach((to, _, _) => afterEachPaths.Add(to.Path));
        router.BeforeEach((to, _, _) => Task.FromResult(
            to.Path == "/old" ? NavigationGuardResult.RedirectTo("/new") : NavigationGuardResult.Allow));

        await router.Push("/old");

        afterEachPaths.ShouldBe(["/new"]);
    }

    [Fact]
    public async Task Push_WhenGuardRedirectsToNamedRoute_ResolvesAndNavigates()
    {
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/old"), new RouteRecord("/users/:id", name: "user")]);
        router.BeforeEach((to, _, _) => Task.FromResult(
            to.Path == "/old"
                ? NavigationGuardResult.RedirectToName("user", RouteParameters.Empty.With("id", "42"))
                : NavigationGuardResult.Allow));

        var failure = await router.Push("/old");

        failure.ShouldBeNull();
        router.CurrentRoute.Value.Path.ShouldBe("/users/42");
    }

    [Fact]
    public async Task Push_WhenRedirectsLoop_ThrowsDescriptiveErrorRoutedToOnError()
    {
        // Mirrors vue-router's infinite-redirect detection: Viu enforces a hard depth cap that throws
        // NavigationRedirectException (routed to onError and faulting the task).
        Exception? captured = null;
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/a"), new RouteRecord("/b")]);
        router.OnError((error, _, _) => captured = error);
        router.BeforeEach((to, _, _) => Task.FromResult(
            NavigationGuardResult.RedirectTo(to.Path == "/a" ? "/b" : "/a")));

        var exception = await Should.ThrowAsync<NavigationRedirectException>(() => router.Push("/a"));

        captured.ShouldBeSameAs(exception);
        router.CurrentRoute.Value.Path.ShouldBe("/");
    }

    [Fact]
    public async Task Push_WhenGuardThrows_RoutesToOnError_FaultsTheTask_AndLeavesRouteUntouched()
    {
        Exception? captured = null;
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        router.OnError((error, _, _) => captured = error);
        var boom = new InvalidOperationException("guard failed");
        router.BeforeEach((_, _, _) => throw boom);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => router.Push("/a"));

        thrown.ShouldBeSameAs(boom);
        captured.ShouldBeSameAs(boom);
        router.CurrentRoute.Value.Path.ShouldBe("/");
    }

    [Fact]
    public async Task Push_RunsPerRouteBeforeEnter_OnlyWhenTheRecordIsNewlyEntered()
    {
        var entered = new List<string>();
        NavigationGuard beforeEnter = (to, _, _) =>
        {
            entered.Add(to.Path);
            return Task.FromResult(NavigationGuardResult.Allow);
        };
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/a", beforeEnter: beforeEnter), new RouteRecord("/b")]);

        await router.Push("/a");
        await router.Push("/b");
        await router.Push("/a");

        // Fires on each fresh entry of /a, never for /b (no per-route guard) and never on the leg away.
        entered.ShouldBe(["/a", "/a"]);
    }

    [Fact]
    public async Task PerRouteBeforeEnter_CanAbortTheNavigation()
    {
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/a", beforeEnter: (_, _, _) => Task.FromResult(NavigationGuardResult.Abort))]);

        var failure = await router.Push("/a");

        failure.ShouldNotBeNull();
        failure.Type.ShouldBe(NavigationFailureType.Aborted);
        router.CurrentRoute.Value.Path.ShouldBe("/");
    }

    [Fact]
    public async Task BeforeEach_RemovalHandle_UnregistersTheGuard()
    {
        // vue-router returns an unregister function from beforeEach; invoking it stops the guard.
        var runs = 0;
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        var remove = router.BeforeEach((_, _, _) =>
        {
            runs++;
            return Task.FromResult(NavigationGuardResult.Allow);
        });

        await router.Push("/a");
        runs.ShouldBe(1);

        remove();
        await router.Push("/b");
        runs.ShouldBe(1);
    }

    [Fact]
    public async Task BeforeResolve_RunsAfterBeforeEach_AndCanAbort()
    {
        var order = new List<string>();
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        router.BeforeEach((_, _, _) =>
        {
            order.Add("beforeEach");
            return Task.FromResult(NavigationGuardResult.Allow);
        });
        router.BeforeResolve((_, _, _) =>
        {
            order.Add("beforeResolve");
            return Task.FromResult(NavigationGuardResult.Abort);
        });

        var failure = await router.Push("/a");

        order.ShouldBe(["beforeEach", "beforeResolve"]);
        failure!.Type.ShouldBe(NavigationFailureType.Aborted);
        router.CurrentRoute.Value.Path.ShouldBe("/");
    }
}
