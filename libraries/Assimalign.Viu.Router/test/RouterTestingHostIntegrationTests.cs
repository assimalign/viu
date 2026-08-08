using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Testing;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Item 2e close-out: one mounted Testing-host application drives the complete navigation outcome
// set plus nested outlet depth and matched-record key retention. Specified by [RTR-4] through
// [RTR-7].
public sealed class RouterTestingHostIntegrationTests
{
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
}
