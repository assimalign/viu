using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Item 2e close-out: one mounted Testing-host application drives the complete navigation outcome
// set plus nested outlet depth and matched-record key retention. Specified by [RTR-4] through
// [RTR-7].
public sealed class RouterTestingHostIntegrationTests
{
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

        (await router.Push("/section/1/details")).ShouldBeNull();
        using Assimalign.Viu.Testing.ComponentWrapper wrapper =
            MountView(router, layout, detail, blocked, allowed);
        ComponentContext? layoutContext = layout.Context;
        ComponentContext? detailContext = detail.Context;
        wrapper.Html().ShouldBe(
            "<div class=\"layout\"><span class=\"value\">1</span></div>");

        NavigationFailure? blockedFailure = await router.Push("/blocked");
        await wrapper.NextTickAsync();
        blockedFailure.ShouldNotBeNull();
        blockedFailure.Type.ShouldBe(NavigationFailureType.Aborted);
        router.CurrentRoute.Value.Path.ShouldBe("/section/1/details");

        (await router.Push("/redirect")).ShouldBeNull();
        await wrapper.NextTickAsync();
        router.CurrentRoute.Value.Path.ShouldBe("/section/2/details");
        wrapper.Html().ShouldBe(
            "<div class=\"layout\"><span class=\"value\">2</span></div>");
        layout.Context.ShouldBeSameAs(layoutContext);
        detail.Context.ShouldBeSameAs(detailContext);
        layout.SetupCount.ShouldBe(1);
        detail.SetupCount.ShouldBe(1);

        (await router.Push("/allowed")).ShouldBeNull();
        await wrapper.NextTickAsync();
        wrapper.Html().ShouldBe("<div class=\"allowed\">allowed</div>");
        layout.IsUnmounted.ShouldBeTrue();
        detail.IsUnmounted.ShouldBeTrue();
        allowed.SetupCount.ShouldBe(1);
    }
}
