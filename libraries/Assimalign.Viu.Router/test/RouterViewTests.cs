using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.RuntimeCore;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Pins RouterView against vue-router's <RouterView> (packages/router/src/RouterView.ts,
// https://router.vuejs.org/api/#Component-RouterView): depth resolution through provide/inject,
// reactive swap on navigation, the "only the affected view re-renders" contract, param-only
// re-render without remount, and the three per-route props forms
// (https://router.vuejs.org/guide/essentials/passing-props.html). All DOM-free through the Testing
// renderer with memory history.
public class RouterViewTests
{
    [Fact]
    public async Task RouterView_RendersMatchedComponent_AndSwapsReactivelyOnNavigation()
    {
        var viewA = LabelView("a");
        var viewB = LabelView("b");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/a", component: viewA),
                new RouteRecord("/b", component: viewB),
            ]);
        _ = router.Push("/a");
        using var wrapper = MountView(router);

        wrapper.Html().ShouldBe("<div class=\"a\">a</div>");
        viewA.RenderCount.ShouldBe(1);

        _ = router.Push("/b");
        await wrapper.NextTickAsync();

        wrapper.Html().ShouldBe("<div class=\"b\">b</div>");
        viewB.SetupCount.ShouldBe(1);
        viewA.Instance!.IsUnmounted.ShouldBeTrue();
    }

    [Fact]
    public void RouterView_NestedViews_ResolveTheMatchedChainByDepth()
    {
        // /users/:id matches [users, :id]; the outer view renders the layout at depth 0 and the
        // layout's nested <RouterView> renders the detail at depth 1 (depth via provide/inject).
        var outlet = new RouterView();
        var detail = LabelView("detail");
        var layout = LayoutView(outlet);
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/users", component: layout, children:
                [
                    new RouteRecord(":id", component: detail),
                ]),
            ]);
        _ = router.Push("/users/1");
        using var wrapper = MountView(router);

        wrapper.Html().ShouldBe("<div class=\"layout\"><div class=\"detail\">detail</div></div>");
        layout.Instance.ShouldNotBeNull();
        detail.Instance.ShouldNotBeNull();
    }

    [Fact]
    public async Task RouterView_LeafOnlyNavigation_ReRendersOnlyTheAffectedView()
    {
        var outlet = new RouterView();
        var layout = LayoutView(outlet);
        var profile = LabelView("profile");
        var settings = LabelView("settings");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/users", component: layout, children:
                [
                    new RouteRecord("profile", component: profile),
                    new RouteRecord("settings", component: settings),
                ]),
            ]);
        _ = router.Push("/users/profile");
        using var wrapper = MountView(router);

        wrapper.Html().ShouldBe("<div class=\"layout\"><div class=\"profile\">profile</div></div>");
        layout.SetupCount.ShouldBe(1);
        layout.RenderCount.ShouldBe(1);
        var layoutInstance = layout.Instance;

        _ = router.Push("/users/settings");
        await wrapper.NextTickAsync();

        wrapper.Html().ShouldBe("<div class=\"layout\"><div class=\"settings\">settings</div></div>");
        // Only the leaf changed: the parent layout view was neither re-rendered nor remounted, and
        // the old leaf was torn down while the new one mounted.
        layout.SetupCount.ShouldBe(1);
        layout.RenderCount.ShouldBe(1);
        layout.Instance.ShouldBeSameAs(layoutInstance);
        profile.Instance!.IsUnmounted.ShouldBeTrue();
        settings.SetupCount.ShouldBe(1);
        settings.RenderCount.ShouldBe(1);
    }

    [Fact]
    public async Task RouterView_ParameterOnlyNavigation_UpdatesPropsWithoutRemounting()
    {
        // vue-router's reactive route object: /users/1 -> /users/2 keeps the same matched record, so
        // the component patches with new props instead of remounting.
        var view = PropView("id");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/users/:id", component: view, propertiesResolver: RouteComponentProperties.FromParameters()),
            ]);
        _ = router.Push("/users/1");
        using var wrapper = MountView(router);

        wrapper.Html().ShouldBe("<span class=\"value\">1</span>");
        view.SetupCount.ShouldBe(1);
        view.RenderCount.ShouldBe(1);
        var instance = view.Instance;

        _ = router.Push("/users/2");
        await wrapper.NextTickAsync();

        wrapper.Html().ShouldBe("<span class=\"value\">2</span>");
        view.Instance.ShouldBeSameAs(instance);
        view.SetupCount.ShouldBe(1);
        view.RenderCount.ShouldBe(2);
    }

    [Fact]
    public void RouterView_PropsTrue_MapsRouteParametersToProps()
    {
        var view = PropView("id");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/users/:id", component: view, propertiesResolver: RouteComponentProperties.FromParameters()),
            ]);
        _ = router.Push("/users/42");
        using var wrapper = MountView(router);

        wrapper.Html().ShouldBe("<span class=\"value\">42</span>");
    }

    [Fact]
    public void RouterView_PropsObject_PassesStaticProps()
    {
        var view = PropView("role");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/admin", component: view, propertiesResolver: RouteComponentProperties.FromValues(("role", "admin"))),
            ]);
        _ = router.Push("/admin");
        using var wrapper = MountView(router);

        wrapper.Html().ShouldBe("<span class=\"value\">admin</span>");
    }

    [Fact]
    public void RouterView_PropsFunction_ReceivesTheResolvedRoute()
    {
        var view = PropView("userId");
        RouteComponentPropertiesResolver resolver =
            route => VirtualNodeFactory.Properties(("userId", route.Parameters.GetString("id")));
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/users/:id", component: view, propertiesResolver: resolver),
            ]);
        _ = router.Push("/users/7");
        using var wrapper = MountView(router);

        wrapper.Html().ShouldBe("<span class=\"value\">7</span>");
    }

    [Fact]
    public void RouterView_RendersNothing_WhenNoRecordMatchesAtItsDepth()
    {
        var router = new Router(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/a", component: LabelView("a")),
            ]);
        _ = router.Push("/nowhere");
        using var wrapper = MountView(router);

        router.CurrentRoute.Value.IsMatched.ShouldBeFalse();
        wrapper.Find("div").ShouldBeNull();
    }
}
