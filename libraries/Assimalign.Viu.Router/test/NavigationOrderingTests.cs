using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Pins the full navigation resolution order and lifecycle-bound in-component guards. The redesigned
// API passes IComponentContext and outlet depth explicitly; no ambient component or injection lookup
// participates in registration.
public class NavigationOrderingTests
{
    [Fact]
    public async Task Navigation_RunsEveryGuard_InVueRouterDocumentedOrder()
    {
        var log = new List<string>();
        TrackingComponent layout = GuardedLayout(log);
        TrackingComponent leafA = GuardedLeaf(log, "a");
        TrackingComponent leafB = MountedLeaf(log, "b");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/section", component: layout.Request, children:
                [
                    new RouteRecord("a", component: leafA.Request),
                    new RouteRecord(
                        "b",
                        component: leafB.Request,
                        beforeEnter: (_, _, _) =>
                        {
                            log.Add("beforeEnter");
                            return Task.FromResult(NavigationGuardResult.Allow);
                        },
                        routeEnterGuard: new LoggingEnterGuard(log)),
                ]),
            ]);

        await router.Push("/section/a");
        using var wrapper = MountView(router, layout, leafA, leafB);
        router.BeforeEach((_, _, _) =>
        {
            log.Add("beforeEach");
            return Task.FromResult(NavigationGuardResult.Allow);
        });
        router.BeforeResolve((_, _, _) =>
        {
            log.Add("beforeResolve");
            return Task.FromResult(NavigationGuardResult.Allow);
        });
        router.AfterEach((_, _, _) => log.Add("afterEach"));
        log.Clear();

        await router.Push("/section/b");
        await wrapper.NextTickAsync();

        log.ShouldBe(
        [
            "beforeRouteLeave",
            "beforeEach",
            "beforeRouteUpdate",
            "beforeEnter",
            "beforeRouteEnter",
            "beforeResolve",
            "afterEach",
            "mounted",
        ]);
        wrapper.Html().ShouldBe(
            "<div class=\"layout\"><div class=\"b\">b</div></div>");
    }

    [Fact]
    public async Task OnBeforeRouteLeave_CanAbortNavigation_KeepingTheCurrentView()
    {
        TrackingComponent blocking = BlockingLeaf();
        TrackingComponent viewB = LabelView("b");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/a", component: blocking.Request),
                new RouteRecord("/b", component: viewB.Request),
            ]);
        await router.Push("/a");
        using var wrapper = MountView(router, blocking, viewB);
        wrapper.Html().ShouldBe("<div class=\"a\">a</div>");

        NavigationFailure? failure = await router.Push("/b");
        await wrapper.NextTickAsync();

        failure.ShouldNotBeNull();
        failure.Type.ShouldBe(NavigationFailureType.Aborted);
        router.CurrentRoute.Value.Path.ShouldBe("/a");
        wrapper.Html().ShouldBe("<div class=\"a\">a</div>");
    }

    [Fact]
    public async Task OnBeforeRouteUpdate_FiresWhenTheSameRecordIsReusedWithNewParameters()
    {
        var updates = new List<string>();
        TrackingComponent view = UpdateTrackingLeaf(updates);
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/users/:id", component: view.Request)]);
        await router.Push("/users/1");
        using var wrapper = MountView(router, view);

        NavigationFailure? failure = await router.Push("/users/2");
        await wrapper.NextTickAsync();

        failure.ShouldBeNull();
        updates.ShouldBe(["/users/2"]);
        router.CurrentRoute.Value.Path.ShouldBe("/users/2");
    }

    [Fact]
    public async Task SameTemplateOnDifferentRecords_RemountsAndMovesLifecycleBoundGuards()
    {
        int leaveRuns = 0;
        var view = new TrackingComponent(
            "shared",
            _ => ComponentTree.Element(
                "div",
                Attributes(("class", "shared")),
                [ComponentTree.Text("shared")]),
            setup: context => RouterGuards.OnBeforeRouteLeave(
                context,
                (_, _, _) =>
                {
                    leaveRuns++;
                    return Task.FromResult(NavigationGuardResult.Allow);
                }));
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/a", component: view.Request),
                new RouteRecord("/b", component: view.Request),
            ]);
        await router.Push("/a");
        using var wrapper = MountView(router, view);
        IComponentContext? firstContext = view.Context;

        await router.Push("/b");
        await wrapper.NextTickAsync();

        leaveRuns.ShouldBe(1);
        view.SetupCount.ShouldBe(2);
        view.Context.ShouldNotBeSameAs(firstContext);

        await router.Push("/a");
        await wrapper.NextTickAsync();

        leaveRuns.ShouldBe(2);
        view.SetupCount.ShouldBe(3);
    }

    private static TrackingComponent GuardedLayout(List<string> log)
    {
        return new TrackingComponent(
            "layout",
            _ => ComponentTree.Element(
                "div",
                Attributes(("class", "layout")),
                [
                    ComponentTree.Template<RouterView>(
                        Arguments(("depth", 1))),
                ]),
            setup: context => RouterGuards.OnBeforeRouteUpdate(
                context,
                (_, _, _) =>
                {
                    log.Add("beforeRouteUpdate");
                    return Task.FromResult(NavigationGuardResult.Allow);
                },
                depth: 0));
    }

    private static TrackingComponent GuardedLeaf(
        List<string> log,
        string label)
    {
        return new TrackingComponent(
            label,
            _ => ComponentTree.Element(
                "div",
                Attributes(("class", label)),
                [ComponentTree.Text(label)]),
            setup: context => RouterGuards.OnBeforeRouteLeave(
                context,
                (_, _, _) =>
                {
                    log.Add("beforeRouteLeave");
                    return Task.FromResult(NavigationGuardResult.Allow);
                },
                depth: 1));
    }

    private static TrackingComponent MountedLeaf(
        List<string> log,
        string label)
    {
        return new TrackingComponent(
            label,
            _ => ComponentTree.Element(
                "div",
                Attributes(("class", label)),
                [ComponentTree.Text(label)]),
            setup: context => context.Lifecycle.OnMounted(
                () => log.Add("mounted")));
    }

    private static TrackingComponent BlockingLeaf()
    {
        return new TrackingComponent(
            "a",
            _ => ComponentTree.Element(
                "div",
                Attributes(("class", "a")),
                [ComponentTree.Text("a")]),
            setup: context => RouterGuards.OnBeforeRouteLeave(
                context,
                (_, _, _) => Task.FromResult(NavigationGuardResult.Abort)));
    }

    private static TrackingComponent UpdateTrackingLeaf(
        List<string> updates)
    {
        return new TrackingComponent(
            "user",
            _ => ComponentTree.Element(
                "div",
                Attributes(("class", "user")),
                [ComponentTree.Text("user")]),
            setup: context => RouterGuards.OnBeforeRouteUpdate(
                context,
                (to, _, _) =>
                {
                    updates.Add(to.Path);
                    return Task.FromResult(NavigationGuardResult.Allow);
                }));
    }

    private sealed class LoggingEnterGuard : IRouteEnterGuard
    {
        private readonly List<string> _log;

        internal LoggingEnterGuard(List<string> log)
        {
            _log = log;
        }

        public Task<NavigationGuardResult> BeforeRouteEnter(
            RouteLocation to,
            RouteLocation from,
            CancellationToken cancellationToken)
        {
            _log.Add("beforeRouteEnter");
            return Task.FromResult(NavigationGuardResult.Allow);
        }
    }
}
