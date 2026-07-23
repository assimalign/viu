using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Testing;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Pins the redesign boundary: router components use only the application-owned IServiceProvider.
// Router ships no service container, builder registration API, or hierarchical fallback.
public class RouterServiceIntegrationTests
{
    [Fact]
    public void RouterView_ResolvesRouterFromTheExplicitApplicationServiceProvider()
    {
        TrackingComponent view = LabelView("service");
        var router = new Router(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/a", component: view.Request)]);
        _ = router.Push("/a");
        var services = new RecordingServiceProvider(router);
        ComponentMountOptions options = OptionsFor(router, view);
        options.Services = services;

        using var wrapper = ViuTest.Mount(new RouterView(), options);

        wrapper.Html().ShouldBe("<div class=\"service\">service</div>");
        services.Requests.ShouldContain(typeof(Router));
    }

    private sealed class RecordingServiceProvider : IServiceProvider
    {
        private readonly Router _router;

        internal RecordingServiceProvider(Router router)
        {
            _router = router;
        }

        internal List<Type> Requests { get; } = [];

        public object? GetService(Type serviceType)
        {
            Requests.Add(serviceType);
            return serviceType == typeof(Router) ? _router : null;
        }
    }
}
