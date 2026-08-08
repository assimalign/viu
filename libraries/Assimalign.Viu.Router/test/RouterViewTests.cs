using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Pins RouterView against the unified component tree: service-provider resolution, explicit outlet
// depth, reactive route swaps, parameter updates without remount, and the three route-argument forms.
public class RouterViewTests
{
    [Fact]
    public async Task RouterView_RendersMatchedComponent_AndSwapsReactivelyOnNavigation()
    {
        TrackingComponent viewA = LabelView("a");
        TrackingComponent viewB = LabelView("b");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/a", component: viewA.Request),
                new RouteRecord("/b", component: viewB.Request),
            ]);
        _ = router.PushAsync("/a");
        using var wrapper = MountView(router, viewA, viewB);

        wrapper.Html().ShouldBe("<div class=\"a\">a</div>");
        viewA.RenderCount.ShouldBe(1);

        _ = router.PushAsync("/b");
        await wrapper.NextTickAsync();

        wrapper.Html().ShouldBe("<div class=\"b\">b</div>");
        viewB.SetupCount.ShouldBe(1);
        viewA.IsUnmounted.ShouldBeTrue();
    }

    [Fact]
    public void RouterView_NestedViews_ResolveTheMatchedChainByExplicitDepth()
    {
        TrackingComponent detail = LabelView("detail");
        TrackingComponent layout = LayoutView(outletDepth: 1);
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/users", component: layout.Request, children:
                [
                    new RouteRecord(":id", component: detail.Request),
                ]),
            ]);
        _ = router.PushAsync("/users/1");
        using var wrapper = MountView(router, layout, detail);

        wrapper.Html().ShouldBe(
            "<div class=\"layout\"><div class=\"detail\">detail</div></div>");
        layout.Context.ShouldNotBeNull();
        detail.Context.ShouldNotBeNull();
    }

    [Fact]
    public async Task RouterView_LeafNavigation_PreservesTheParentTemplateInstance()
    {
        TrackingComponent layout = LayoutView(outletDepth: 1);
        TrackingComponent profile = LabelView("profile");
        TrackingComponent settings = LabelView("settings");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/users", component: layout.Request, children:
                [
                    new RouteRecord("profile", component: profile.Request),
                    new RouteRecord("settings", component: settings.Request),
                ]),
            ]);
        _ = router.PushAsync("/users/profile");
        using var wrapper = MountView(router, layout, profile, settings);

        wrapper.Html().ShouldBe(
            "<div class=\"layout\"><div class=\"profile\">profile</div></div>");
        ComponentContext? layoutContext = layout.Context;

        _ = router.PushAsync("/users/settings");
        await wrapper.NextTickAsync();

        wrapper.Html().ShouldBe(
            "<div class=\"layout\"><div class=\"settings\">settings</div></div>");
        layout.SetupCount.ShouldBe(1);
        layout.Context.ShouldBeSameAs(layoutContext);
        profile.IsUnmounted.ShouldBeTrue();
        settings.SetupCount.ShouldBe(1);
    }

    [Fact]
    public async Task RouterView_ParameterOnlyNavigation_UpdatesArgumentsWithoutRemounting()
    {
        TrackingComponent view = PropView("id");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord(
                    "/users/:id",
                    component: view.Request,
                    argumentsResolver: RouteComponentArguments.FromParameters()),
            ]);
        _ = router.PushAsync("/users/1");
        using var wrapper = MountView(router, view);

        wrapper.Html().ShouldBe("<span class=\"value\">1</span>");
        ComponentContext? context = view.Context;

        _ = router.PushAsync("/users/2");
        await wrapper.NextTickAsync();

        wrapper.Html().ShouldBe("<span class=\"value\">2</span>");
        view.Context.ShouldBeSameAs(context);
        view.SetupCount.ShouldBe(1);
        view.RenderCount.ShouldBe(2);
    }

    [Fact]
    public void RouterView_ArgumentsFromParameters_MapsRouteParameters()
    {
        TrackingComponent view = PropView("id");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord(
                    "/users/:id",
                    component: view.Request,
                    argumentsResolver: RouteComponentArguments.FromParameters()),
            ]);
        _ = router.PushAsync("/users/42");
        using var wrapper = MountView(router, view);

        wrapper.Html().ShouldBe("<span class=\"value\">42</span>");
    }

    [Fact]
    public void RouterView_ArgumentsFromValues_PassesStaticArguments()
    {
        TrackingComponent view = PropView("role");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord(
                    "/admin",
                    component: view.Request,
                    argumentsResolver:
                        RouteComponentArguments.FromValues(("role", "admin"))),
            ]);
        _ = router.PushAsync("/admin");
        using var wrapper = MountView(router, view);

        wrapper.Html().ShouldBe("<span class=\"value\">admin</span>");
    }

    [Fact]
    public void RouterView_ArgumentFunction_ReceivesTheResolvedRoute()
    {
        TrackingComponent view = PropView("userId");
        RouteComponentArgumentsResolver resolver =
            route => Arguments(
                ("userId", route.Parameters.GetString("id")));
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord(
                    "/users/:id",
                    component: view.Request,
                    argumentsResolver: resolver),
            ]);
        _ = router.PushAsync("/users/7");
        using var wrapper = MountView(router, view);

        wrapper.Html().ShouldBe("<span class=\"value\">7</span>");
    }

    [Fact]
    public void RouterView_RendersNothing_WhenNoRecordMatchesAtItsDepth()
    {
        TrackingComponent view = LabelView("a");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/a", component: view.Request)]);
        _ = router.PushAsync("/nowhere");
        using var wrapper = MountView(router, view);

        router.CurrentRoute.Value.IsMatched.ShouldBeFalse();
        wrapper.Find("div").ShouldBeNull();
    }

    [Fact]
    public void RouterView_RendersNothing_WhenRouterServiceIsMissing()
    {
        using var wrapper = Assimalign.Viu.Testing.ViuTest.Mount(
            RouterView.Registration);

        wrapper.Find("div").ShouldBeNull();
    }

    [Fact]
    public void RouteRecord_ArgumentsResolverForPrimitiveComponent_Throws()
    {
        Should.Throw<System.ArgumentException>(
            () => new RouteRecord(
                "/text",
                component: Text("text"),
                argumentsResolver:
                    RouteComponentArguments.FromValues(("value", "unused"))));
    }
}
