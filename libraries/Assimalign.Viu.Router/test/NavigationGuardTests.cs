using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins the guarded navigation pipeline ([V01.01.08.04], specified by [RTR-5] and [RTR-6]): the
// awaitable push/replace result, allow/abort/duplicate/redirect outcomes, the redirect loop cap,
// per-route before-enter, removal handles, and OnError routing. All DOM-free through memory history
// with no mounted view.
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
    public void NavigationGuardResult_FactoriesExposeDiscriminatedOutcomes()
    {
        NavigationGuardResult.Allow.OutcomeKind.ShouldBe(NavigationGuardOutcomeKind.Allowed);
        NavigationGuardResult.Allow.FailureReason.ShouldBeNull();
        NavigationGuardResult.Allow.RedirectTarget.ShouldBeNull();

        NavigationGuardResult.Abort.OutcomeKind.ShouldBe(NavigationGuardOutcomeKind.Failed);
        NavigationGuardResult.Abort.FailureReason.ShouldBe(NavigationFailureType.Aborted);
        NavigationGuardResult.Abort.RedirectTarget.ShouldBeNull();

        NavigationGuardResult location = NavigationGuardResult.RedirectTo("/next");
        NavigationRedirectTarget locationTarget = location.RedirectTarget.ShouldNotBeNull();
        location.OutcomeKind.ShouldBe(NavigationGuardOutcomeKind.Redirected);
        location.FailureReason.ShouldBeNull();
        locationTarget.Kind.ShouldBe(NavigationRedirectTargetKind.Location);
        locationTarget.Value.ShouldBe("/next");
        locationTarget.Parameters.ShouldBeSameAs(RouteParameters.Empty);

        RouteParameters parameters = RouteParameters.Empty.With("id", "42");
        NavigationGuardResult named = NavigationGuardResult.RedirectToName("user", parameters);
        NavigationRedirectTarget namedTarget = named.RedirectTarget.ShouldNotBeNull();
        named.OutcomeKind.ShouldBe(NavigationGuardOutcomeKind.Redirected);
        named.FailureReason.ShouldBeNull();
        namedTarget.Kind.ShouldBe(NavigationRedirectTargetKind.NamedRoute);
        namedTarget.Value.ShouldBe("user");
        namedTarget.Parameters.ShouldBeSameAs(parameters);
    }

    [Fact]
    public async Task PushAsync_RunsGlobalBeforeEach_AndConfirmsOnAllow()
    {
        var log = new List<string>();
        var history = RouterHistory.CreateMemory();
        var router = new Router(history, Routes());
        router.BeforeEach((to, _, _) =>
        {
            log.Add(to.Path);
            return Task.FromResult(NavigationGuardResult.Allow);
        });

        var failure = await router.PushAsync("/a");

        failure.ShouldBeNull();
        router.CurrentRoute.Value.Path.ShouldBe("/a");
        history.Location.ShouldBe("/a");
        log.ShouldBe(["/a"]);
    }

    [Fact]
    public async Task PushAsync_WhenBeforeGuardAborts_LeavesRouteAndHistoryUntouched_AndReturnsAbortedFailure()
    {
        // Returning Abort stops the navigation, CurrentRoute is untouched, and PushAsync completes with the
        // failure rather than throwing.
        var history = RouterHistory.CreateMemory();
        var router = new Router(history, Routes());
        router.BeforeEach((_, _, _) => Task.FromResult(NavigationGuardResult.Abort));

        var failure = await router.PushAsync("/a");

        failure.ShouldNotBeNull();
        failure.Type.ShouldBe(NavigationFailureType.Aborted);
        router.CurrentRoute.Value.Path.ShouldBe("/");
        history.Location.ShouldBe("/");
    }

    [Fact]
    public async Task PushAsync_ToCurrentLocation_ReportsDuplicated_WithoutRunningTheGuardChain()
    {
        var beforeEachRuns = 0;
        var router = new Router(RouterHistory.CreateMemory(), Routes());
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
        router.CurrentRoute.Value.Path.ShouldBe("/a");
    }

    [Fact]
    public async Task AfterEach_ObservesBothSuccessAndFailure()
    {
        var observed = new List<(string Path, NavigationFailureType? Failure)>();
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        router.AfterEach((to, _, failure) => observed.Add((to.Path, failure?.Type)));

        await router.PushAsync("/a");
        var removeAbort = router.BeforeEach((_, _, _) => Task.FromResult(NavigationGuardResult.Abort));
        await router.PushAsync("/b");
        removeAbort();

        observed.ShouldBe([("/a", null), ("/b", NavigationFailureType.Aborted)]);
    }

    [Fact]
    public async Task PushAsync_WhenGuardRedirects_RestartsThePipelineAgainstTheNewTarget()
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

        var failure = await router.PushAsync("/old");

        failure.ShouldBeNull();
        router.CurrentRoute.Value.Path.ShouldBe("/new");
        visited.ShouldBe(["/old", "/new"]);
    }

    [Fact]
    public async Task PushAsync_WhenGuardRedirects_FiresAfterEachForTheFinalTargetOnly()
    {
        // The redirect recurses before the after-hooks run, so the intermediate navigation does
        // not surface an afterEach — only the confirmed final one does.
        var afterEachPaths = new List<string>();
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/old"), new RouteRecord("/new")]);
        router.AfterEach((to, _, _) => afterEachPaths.Add(to.Path));
        router.BeforeEach((to, _, _) => Task.FromResult(
            to.Path == "/old" ? NavigationGuardResult.RedirectTo("/new") : NavigationGuardResult.Allow));

        await router.PushAsync("/old");

        afterEachPaths.ShouldBe(["/new"]);
    }

    [Fact]
    public async Task PushAsync_WhenGuardRedirectsToNamedRoute_ResolvesAndNavigates()
    {
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/old"), new RouteRecord("/users/:id", name: "user")]);
        router.BeforeEach((to, _, _) => Task.FromResult(
            to.Path == "/old"
                ? NavigationGuardResult.RedirectToName("user", RouteParameters.Empty.With("id", "42"))
                : NavigationGuardResult.Allow));

        var failure = await router.PushAsync("/old");

        failure.ShouldBeNull();
        router.CurrentRoute.Value.Path.ShouldBe("/users/42");
    }

    [Fact]
    public async Task PushAsync_WhenRedirectsLoop_ThrowsDescriptiveErrorRoutedToOnError()
    {
        // Infinite-redirect detection: a hard depth cap that throws
        // NavigationRedirectException (routed to onError and faulting the task).
        Exception? captured = null;
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/a"), new RouteRecord("/b")]);
        router.OnError((error, _, _) => captured = error);
        router.BeforeEach((to, _, _) => Task.FromResult(
            NavigationGuardResult.RedirectTo(to.Path == "/a" ? "/b" : "/a")));

        var exception = await Should.ThrowAsync<NavigationRedirectException>(() => router.PushAsync("/a"));

        captured.ShouldBeSameAs(exception);
        router.CurrentRoute.Value.Path.ShouldBe("/");
    }

    [Fact]
    public async Task PushAsync_WhenGuardThrows_RoutesToOnError_FaultsTheTask_AndLeavesRouteUntouched()
    {
        Exception? captured = null;
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        router.OnError((error, _, _) => captured = error);
        var boom = new InvalidOperationException("guard failed");
        router.BeforeEach((_, _, _) => throw boom);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => router.PushAsync("/a"));

        thrown.ShouldBeSameAs(boom);
        captured.ShouldBeSameAs(boom);
        router.CurrentRoute.Value.Path.ShouldBe("/");
    }

    [Fact]
    public async Task PushAsync_RunsPerRouteBeforeEnter_OnlyWhenTheRecordIsNewlyEntered()
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

        await router.PushAsync("/a");
        await router.PushAsync("/b");
        await router.PushAsync("/a");

        // Fires on each fresh entry of /a, never for /b (no per-route guard) and never on the leg away.
        entered.ShouldBe(["/a", "/a"]);
    }

    [Fact]
    public async Task PerRouteBeforeEnter_CanAbortTheNavigation()
    {
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/a", beforeEnter: (_, _, _) => Task.FromResult(NavigationGuardResult.Abort))]);

        var failure = await router.PushAsync("/a");

        failure.ShouldNotBeNull();
        failure.Type.ShouldBe(NavigationFailureType.Aborted);
        router.CurrentRoute.Value.Path.ShouldBe("/");
    }

    [Fact]
    public async Task BeforeEach_RemovalHandle_UnregistersTheGuard()
    {
        // BeforeEach returns an unregister delegate; invoking it stops the guard.
        var runs = 0;
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        var remove = router.BeforeEach((_, _, _) =>
        {
            runs++;
            return Task.FromResult(NavigationGuardResult.Allow);
        });

        await router.PushAsync("/a");
        runs.ShouldBe(1);

        remove();
        await router.PushAsync("/b");
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

        var failure = await router.PushAsync("/a");

        order.ShouldBe(["beforeEach", "beforeResolve"]);
        failure!.Type.ShouldBe(NavigationFailureType.Aborted);
        router.CurrentRoute.Value.Path.ShouldBe("/");
    }
}
