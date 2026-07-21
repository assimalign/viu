using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Pins the full navigation resolution order and the in-component guards ([V01.01.08.04]) against
// vue-router's "The Full Navigation Resolution Flow"
// (https://router.vuejs.org/guide/advanced/navigation-guards.html#The-Full-Navigation-Resolution-Flow
// and packages/router/src/navigationGuards.ts). Exercised DOM-free through the Testing renderer with
// memory history: real RouterView mounts contribute leave/update/enter guards through the component
// lifecycle, and an ordering log records every hook.
public class NavigationOrderingTests
{
    [Fact]
    public async Task Navigation_RunsEveryGuard_InVueRouterDocumentedOrder()
    {
        var log = new List<string>();
        var outlet = new RouterView();
        var layout = new UpdateGuardLayout(log, outlet);
        var leafA = new LeaveGuardView(log, "a");
        var leafB = new EnterGuardView(log, "b");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/section", component: layout, children:
                [
                    new RouteRecord("a", component: leafA),
                    new RouteRecord("b", component: leafB, beforeEnter: (_, _, _) =>
                    {
                        log.Add("beforeEnter");
                        return Task.FromResult(NavigationGuardResult.Allow);
                    }),
                ]),
            ]);

        await router.Push("/section/a");
        using var wrapper = MountView(router);

        // Register the global guards after the initial mount, then clear so the log captures only the
        // /section/a -> /section/b navigation. leafA's leave guard and the layout's update guard were
        // registered during mount and fire during that navigation.
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
            "beforeRouteLeave",  // leaving record (deepest child first)
            "beforeEach",        // global
            "beforeRouteUpdate", // reused (updating) record
            "beforeEnter",       // per-route, entering record
            "beforeRouteEnter",  // in-component, entering record
            "beforeResolve",     // global
            "afterEach",         // navigation confirmed
            "mounted",           // DOM update / lifecycle, after confirmation
        ]);
        wrapper.Html().ShouldBe("<div class=\"layout\"><div class=\"b\">b</div></div>");
    }

    [Fact]
    public async Task OnBeforeRouteLeave_CanAbortNavigation_KeepingTheCurrentView()
    {
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/a", component: new BlockingLeaveView()),
                new RouteRecord("/b", component: LabelView("b")),
            ]);
        await router.Push("/a");
        using var wrapper = MountView(router);
        wrapper.Html().ShouldBe("<div class=\"a\">a</div>");

        var failure = await router.Push("/b");
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
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/users/:id", component: new UpdateTrackingView(updates))]);
        await router.Push("/users/1");
        using var wrapper = MountView(router);

        var failure = await router.Push("/users/2");
        await wrapper.NextTickAsync();

        failure.ShouldBeNull();
        updates.ShouldBe(["/users/2"]);
        router.CurrentRoute.Value.Path.ShouldBe("/users/2");
    }

    // A layout rendering <div class="layout"><outlet/></div> that registers a beforeRouteUpdate guard.
    private sealed class UpdateGuardLayout : IComponentDefinition
    {
        private readonly List<string> _log;
        private readonly RouterView _outlet;

        public UpdateGuardLayout(List<string> log, RouterView outlet)
        {
            _log = log;
            _outlet = outlet;
        }

        public string? Name => "layout";

        public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        {
            RouterGuards.OnBeforeRouteUpdate((_, _, _) =>
            {
                _log.Add("beforeRouteUpdate");
                return Task.FromResult(NavigationGuardResult.Allow);
            });
            return () => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Properties(("class", "layout")),
                VirtualNodeFactory.Component(_outlet));
        }
    }

    // A leaf view that registers a beforeRouteLeave guard.
    private sealed class LeaveGuardView : IComponentDefinition
    {
        private readonly List<string> _log;
        private readonly string _label;

        public LeaveGuardView(List<string> log, string label)
        {
            _log = log;
            _label = label;
        }

        public string? Name => _label;

        public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        {
            RouterGuards.OnBeforeRouteLeave((_, _, _) =>
            {
                _log.Add("beforeRouteLeave");
                return Task.FromResult(NavigationGuardResult.Allow);
            });
            return () => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Properties(("class", _label)),
                _label);
        }
    }

    // A leaf view that contributes a beforeRouteEnter guard (interface-based) and logs when mounted.
    private sealed class EnterGuardView : IComponentDefinition, IRouteEnterGuard
    {
        private readonly List<string> _log;
        private readonly string _label;

        public EnterGuardView(List<string> log, string label)
        {
            _log = log;
            _label = label;
        }

        public string? Name => _label;

        public Task<NavigationGuardResult> BeforeRouteEnter(RouteLocation to, RouteLocation from, CancellationToken cancellationToken)
        {
            _log.Add("beforeRouteEnter");
            return Task.FromResult(NavigationGuardResult.Allow);
        }

        public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        {
            Lifecycle.OnMounted(() => _log.Add("mounted"));
            return () => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Properties(("class", _label)),
                _label);
        }
    }

    // A view whose beforeRouteLeave guard aborts every navigation away from it.
    private sealed class BlockingLeaveView : IComponentDefinition
    {
        public string? Name => "a";

        public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        {
            RouterGuards.OnBeforeRouteLeave((_, _, _) => Task.FromResult(NavigationGuardResult.Abort));
            return () => VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("class", "a")), "a");
        }
    }

    // A view that records every beforeRouteUpdate target it is reused for.
    private sealed class UpdateTrackingView : IComponentDefinition
    {
        private readonly List<string> _updates;

        public UpdateTrackingView(List<string> updates) => _updates = updates;

        public string? Name => "user";

        public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        {
            RouterGuards.OnBeforeRouteUpdate((to, _, _) =>
            {
                _updates.Add(to.Path);
                return Task.FromResult(NavigationGuardResult.Allow);
            });
            return () => VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("class", "user")), "user");
        }
    }
}
