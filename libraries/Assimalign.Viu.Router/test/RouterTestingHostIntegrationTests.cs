using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Testing;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Item 2e close-out: one mounted Testing-host application drives the complete navigation outcome
// set plus nested outlet depth and matched-record key retention. Specified by [RTR-4] through
// [RTR-7].
public sealed class RouterTestingHostIntegrationTests
{
    [Fact]
    public async Task Navigate_TransitionWrappedAliasedBlock_PatchesEveryOccurrenceAndSwapsViews()
    {
        ComponentRegistration firstRegistration = new(
            ComponentReference.ForType(typeof(FirstRouteView)),
            new ComponentContract(displayName: nameof(FirstRouteView)),
            static _ => new FirstRouteView());
        ComponentRegistration repeatedRegistration = new(
            ComponentReference.ForType(typeof(TransitionRepeatedRouteView)),
            new ComponentContract(
                renderCacheSize: 1,
                displayName: nameof(TransitionRepeatedRouteView)),
            static _ => new TransitionRepeatedRouteView());
        Router router = new(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord(
                    "/first",
                    component: new ComponentNode(firstRegistration.Reference)),
                new RouteRecord(
                    "/repeated",
                    component: new ComponentNode(repeatedRegistration.Reference)),
            ]);
        (await router.PushAsync("/first")).ShouldBeNull();
        var shell = new TransitionRouterShell(router);
        using ComponentWrapper wrapper = ComponentTest.Mount(
            shell,
            OptionsForRegistrations(
                router,
                firstRegistration,
                repeatedRegistration));
        ComponentWrapper firstWrapper = wrapper.GetComponent<FirstRouteView>();
        FirstRouteView firstInstance = firstWrapper.Instance.ShouldBeOfType<FirstRouteView>();
        await wrapper.NextTickAsync();
        shell.TransitionCalls.Clear();

        (await router.PushAsync("/repeated")).ShouldBeNull();
        await wrapper.NextTickAsync();

        firstWrapper.Exists().ShouldBeFalse();
        firstInstance.UnmountCount.ShouldBe(1);
        ComponentWrapper repeatedWrapper =
            wrapper.GetComponent<TransitionRepeatedRouteView>();
        TransitionRepeatedRouteView repeatedInstance = repeatedWrapper.Instance
            .ShouldBeOfType<TransitionRepeatedRouteView>();
        IReadOnlyList<ElementWrapper> signals = wrapper.FindAll("signal");
        signals.Count.ShouldBe(3);
        signals[0].Attribute("data-state").ShouldBe("changed");
        signals[1].Attribute("data-state").ShouldBe("cached");
        signals[2].Attribute("data-state").ShouldBe("cached");
        shell.TransitionCalls.ShouldBe(
        [
            "beforeLeave",
            "leave",
            "afterLeave",
            "beforeEnter",
            "enter",
            "afterEnter",
        ]);

        (await router.PushAsync("/first")).ShouldBeNull();
        await wrapper.NextTickAsync();

        repeatedWrapper.Exists().ShouldBeFalse();
        repeatedInstance.UnmountCount.ShouldBe(1);
        wrapper.FindAll("signal").ShouldBeEmpty();
        wrapper.GetComponent<FirstRouteView>().Exists().ShouldBeTrue();
    }

    [Fact]
    public async Task Navigate_DistinctRouteViewComponentsWithRepeatedCachedNode_UnmountsPreviousAndMountsNext()
    {
        ComponentRegistration firstRegistration = new(
            ComponentReference.ForType(typeof(FirstRouteView)),
            new ComponentContract(
                renderCacheSize: 0,
                displayName: nameof(FirstRouteView)),
            static _ => new FirstRouteView());
        ComponentRegistration repeatedRegistration = new(
            ComponentReference.ForType(typeof(RepeatedCachedRouteView)),
            new ComponentContract(
                renderCacheSize: 1,
                displayName: nameof(RepeatedCachedRouteView)),
            static _ => new RepeatedCachedRouteView());
        Router router = new(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord(
                    "/first",
                    component: new ComponentNode(firstRegistration.Reference)),
                new RouteRecord(
                    "/repeated",
                    component: new ComponentNode(repeatedRegistration.Reference)),
            ]);

        (await router.PushAsync("/first")).ShouldBeNull();
        using ComponentWrapper wrapper = MountRegisteredView(
            router,
            firstRegistration,
            repeatedRegistration);
        ComponentWrapper firstWrapper = wrapper.GetComponent<FirstRouteView>();
        FirstRouteView firstInstance = firstWrapper.Instance.ShouldBeOfType<FirstRouteView>();
        firstWrapper.Exists().ShouldBeTrue();
        wrapper.FindComponent<RepeatedCachedRouteView>().ShouldBeNull();
        wrapper.Html().ShouldBe(
            "<section class=\"first-route\">first</section>");

        (await router.PushAsync("/repeated")).ShouldBeNull();
        await wrapper.NextTickAsync();

        firstWrapper.Exists().ShouldBeFalse();
        firstWrapper.Html().ShouldBeEmpty();
        firstInstance.UnmountCount.ShouldBe(1);
        wrapper.FindComponent<FirstRouteView>().ShouldBeNull();
        ComponentWrapper repeatedWrapper =
            wrapper.GetComponent<RepeatedCachedRouteView>();
        repeatedWrapper.Exists().ShouldBeTrue();
        repeatedWrapper.Context.ShouldNotBeSameAs(firstWrapper.Context);
        repeatedWrapper.Instance.ShouldBeOfType<RepeatedCachedRouteView>()
            .SetupCount.ShouldBe(1);
        wrapper.Html().ShouldBe(
            "<ul class=\"repeated-route\"><li><span class=\"signal-dot\"></span></li>" +
            "<li><span class=\"signal-dot\"></span></li>" +
            "<li><span class=\"signal-dot\"></span></li></ul>");
    }

    [Fact]
    public async Task Navigate_BlockAllowRedirect_NestedDepthAndKeys_ComposeEndToEnd()
    {
        TrackingComponent layout = LayoutView(outletDepth: 1);
        TrackingComponent detail = PropView("id");
        TrackingComponent blocked = LabelView("blocked");
        TrackingComponent allowed = LabelView("allowed");
        Router router = new(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/section/:id", component: layout.Request, children:
                [
                    new RouteRecord(
                        "details",
                        component: detail.Request,
                        argumentsResolver: RouteComponentArguments.FromParameters()),
                ]),
                new RouteRecord("/blocked", component: blocked.Request),
                new RouteRecord("/redirect"),
                new RouteRecord("/allowed", component: allowed.Request),
            ]);
        router.BeforeEach((to, _, _) => Task.FromResult(
            to.Path switch
            {
                "/blocked" => NavigationGuardResult.Abort,
                "/redirect" => NavigationGuardResult.RedirectTo(
                    "/section/2/details"),
                _ => NavigationGuardResult.Allow,
            }));

        (await router.PushAsync("/section/1/details")).ShouldBeNull();
        using Assimalign.Viu.Testing.ComponentWrapper wrapper =
            MountView(router, layout, detail, blocked, allowed);
        ComponentContext? layoutContext = layout.Context;
        ComponentContext? detailContext = detail.Context;
        wrapper.Html().ShouldBe(
            "<div class=\"layout\"><span class=\"value\">1</span></div>");

        NavigationFailure? blockedFailure = await router.PushAsync("/blocked");
        await wrapper.NextTickAsync();
        blockedFailure.ShouldNotBeNull();
        blockedFailure.Type.ShouldBe(NavigationFailureType.Aborted);
        router.CurrentRoute.Value.Path.ShouldBe("/section/1/details");

        (await router.PushAsync("/redirect")).ShouldBeNull();
        await wrapper.NextTickAsync();
        router.CurrentRoute.Value.Path.ShouldBe("/section/2/details");
        wrapper.Html().ShouldBe(
            "<div class=\"layout\"><span class=\"value\">2</span></div>");
        layout.Context.ShouldBeSameAs(layoutContext);
        detail.Context.ShouldBeSameAs(detailContext);
        layout.SetupCount.ShouldBe(1);
        detail.SetupCount.ShouldBe(1);

        (await router.PushAsync("/allowed")).ShouldBeNull();
        await wrapper.NextTickAsync();
        wrapper.Html().ShouldBe("<div class=\"allowed\">allowed</div>");
        layout.IsUnmounted.ShouldBeTrue();
        detail.IsUnmounted.ShouldBeTrue();
        allowed.SetupCount.ShouldBe(1);
    }

    private sealed class FirstRouteView : IComponent
    {
        internal int UnmountCount { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Lifecycle.OnUnmounted(() => UnmountCount++);
            return static _ => Element(
                "section",
                Attributes(("class", "first-route")),
                [Text("first")]);
        }
    }

    private sealed class RepeatedCachedRouteView : IComponent
    {
        internal int SetupCount { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            SetupCount++;
            return frame =>
            {
                // [V01.01.15.02]/[SFC-OPT-1]: generated v-for output can present one cached
                // immutable static node in several live positions during one component mount.
                ElementNode cachedDot = frame.GetOrAddCache(
                    0,
                    static () => new ElementNode(
                        new QualifiedName("span"),
                        bindings: Attributes(("class", "signal-dot")),
                        renderPlan: new RenderPlan(PatchFlags.Cached)));
                return Element(
                    "ul",
                    Attributes(("class", "repeated-route")),
                    [
                        Element("li", children: [cachedDot]),
                        Element("li", children: [cachedDot]),
                        Element("li", children: [cachedDot]),
                    ]);
            };
        }
    }

    private sealed class TransitionRouterShell : IComponent
    {
        private readonly Router _router;

        internal TransitionRouterShell(Router router)
        {
            _router = router;
        }

        internal List<string> TransitionCalls { get; } = [];

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ =>
            {
                string path = _router.CurrentRoute.Value.Path;
                var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [TransitionProperties.ResolvedArgument] = new TransitionProperties
                    {
                        Mode = "out-in",
                        OnBeforeEnter = _ => TransitionCalls.Add("beforeEnter"),
                        OnEnter = (_, complete) =>
                        {
                            TransitionCalls.Add("enter");
                            complete();
                        },
                        OnAfterEnter = _ => TransitionCalls.Add("afterEnter"),
                        OnBeforeLeave = _ => TransitionCalls.Add("beforeLeave"),
                        OnLeave = (_, complete) =>
                        {
                            TransitionCalls.Add("leave");
                            complete();
                        },
                        OnAfterLeave = _ => TransitionCalls.Add("afterLeave"),
                    },
                };
                var slots = new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
                {
                    ["default"] = _ => new ElementNode(
                        new QualifiedName("section"),
                        children: [new ComponentNode(RouterView.Registration.Reference)],
                        key: path),
                };
                return new TransitionNode(new ComponentInvocation(arguments, slots));
            };
        }
    }

    private sealed class TransitionRepeatedRouteView : IComponent
    {
        private readonly Reference<bool> _replaceFirst = Reactive.Reference(false);

        internal int UnmountCount { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Lifecycle.OnMounted(() => _replaceFirst.Value = true);
            context.Lifecycle.OnUnmounted(() => UnmountCount++);
            return frame =>
            {
                ElementNode cached = frame.GetOrAddCache(
                    0,
                    static () => new ElementNode(
                        new QualifiedName("signal"),
                        bindings: Attributes(("data-state", "cached")),
                        renderPlan: new RenderPlan(PatchFlags.Cached)));
                VirtualNode first = _replaceFirst.Value
                    ? new ElementNode(
                        new QualifiedName("signal"),
                        bindings: Attributes(("data-state", "changed")),
                        renderPlan: new RenderPlan(PatchFlags.FullProperties))
                    : cached;
                VirtualNode[] signals = [first, cached, cached];
                var rows = new List<VirtualNode>(signals.Length);
                for (int index = 0; index < signals.Length; index++)
                {
                    rows.Add(
                        new ElementNode(
                            new QualifiedName("row"),
                            children: [signals[index]],
                            key: index));
                }

                return new ElementNode(
                    new QualifiedName("list"),
                    children: rows,
                    renderPlan: new RenderPlan(
                        PatchFlags.NeedPatch,
                        dynamicChildren: signals));
            };
        }
    }
}
