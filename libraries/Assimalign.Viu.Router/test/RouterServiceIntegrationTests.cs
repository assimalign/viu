using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Pins the additive service-based router path ([V01.01.03.24]): AddRouter registers the router through
// the app service provider (and keeps the app-wide provide parity), and RouterView/RouterLink resolve
// service-first-then-provide. The existing provide-only path (RouterViewTests) is unchanged.
public class RouterServiceIntegrationTests
{
    // A recording IApplicationBuilder that captures Provide and exposes a real service container.
    private sealed class RecordingApplicationBuilder : IApplicationBuilder
    {
        public IServiceContainer Services { get; } = new ServiceContainer();

        public Dictionary<object, object?> Provided { get; } = [];

        public IApplicationBuilder Use(IApplicationPlugin plugin) => this;

        public IApplicationBuilder Provide<T>(InjectionKey<T> key, T value)
        {
            Provided[key] = value;
            return this;
        }

        public IApplicationBuilder Provide(string key, object? value)
        {
            Provided[key] = value;
            return this;
        }

        public IApplicationBuilder Component(string name, IComponent definition) => this;

        public IApplicationBuilder Directive(string name, IDirective directive) => this;

        public IApplicationBuilder ConfigureApplication(Action<IApplicationContext> configure) => this;

        public IApplicationBuilder UseServiceContainer(IServiceContainer services) => this;

        public IApplicationBuilder ConfigureServices(Action<IServiceContainer> configure)
        {
            configure(Services);
            return this;
        }

        public IApplication Build() => throw new NotSupportedException();
    }

    private static ComponentWrapper MountViewWithServices(Router router)
    {
        var services = new ServiceContainer().AddSingleton(router).Build();
        return ViuTest.Mount(new RouterView(), new ComponentMountOptions { Services = services });
    }

    [Fact]
    public void AddRouter_RegistersRouterAsAService_AndProvidesItAppWide()
    {
        var router = new Router(RouterHistory.CreateMemory(), [new RouteRecord("/a", component: LabelView("a"))]);
        var builder = new RecordingApplicationBuilder();

        builder.AddRouter(router);

        // Service path: the router resolves from the built provider.
        builder.Services.Build().GetRequiredService<Router>().ShouldBeSameAs(router);
        // Provide-path parity: the router is provided under the router injection key.
        builder.Provided[RouterInjectionKeys.Router].ShouldBeSameAs(router);
    }

    [Fact]
    public void RouterView_ResolvesTheRouterFromServices_AndRendersTheMatchedRoute()
    {
        var view = LabelView("a");
        var router = new Router(RouterHistory.CreateMemory(), [new RouteRecord("/a", component: view)]);
        _ = router.Push("/a");

        using var wrapper = MountViewWithServices(router);

        // Same rendered outcome as the provide path, but the router was resolved from IServiceProvider.
        wrapper.Html().ShouldBe("<div class=\"a\">a</div>");
        view.RenderCount.ShouldBe(1);
    }

    [Fact]
    public void RouterView_PrefersTheServiceRouter_OverAProvidedRouter()
    {
        var serviceRouter = new Router(RouterHistory.CreateMemory(), [new RouteRecord("/a", component: LabelView("service"))]);
        var provideRouter = new Router(RouterHistory.CreateMemory(), [new RouteRecord("/a", component: LabelView("provide"))]);
        _ = serviceRouter.Push("/a");
        _ = provideRouter.Push("/a");

        var services = new ServiceContainer().AddSingleton(serviceRouter).Build();
        var options = new ComponentMountOptions { Services = services };
        options.Provide(RouterInjectionKeys.Router, provideRouter);

        using var wrapper = ViuTest.Mount(new RouterView(), options);

        // Service-first: the service router's route rendered, not the provided router's.
        wrapper.Html().ShouldBe("<div class=\"service\">service</div>");
    }
}
