using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Pins the borrowed, independently supplied <see cref="IServiceProvider"/> application boundary.
/// </summary>
public class ServerRendererServiceTests
{
    [Fact]
    public void Builder_ConfigureApplication_FreezesCompositionAndDiagnosticsAtBuild()
    {
        InlineComponent component = new(_ => () => TestTree.Element("div", "x"));
        IComponent rootComponent = component.Request();
        TestServiceProvider services = TestServiceProvider.Empty;
        Action<string> warnHandler = static _ => { };
        ApplicationOptions? configuredOptions = null;
        ServerApplicationBuilder builder = new();

        ServerApplicationBuilder configured = builder.ConfigureApplication(options =>
        {
            configuredOptions = options;
            options.RootComponent = rootComponent;
            options.Components = InlineComponentFactory.Instance;
            options.Services = services;
            options.WarnHandler = warnHandler;
        });
        ServerRenderApplication application = configured.Build();
        configuredOptions!.RootComponent = ComponentTree.Comment("changed");
        configuredOptions.Services = new TestServiceProvider(
            new Dictionary<Type, object>());
        configuredOptions.WarnHandler = null;

        // [APP-2] Build freezes the option values while the supplied dependencies stay borrowed.
        configured.ShouldBeSameAs(builder);
        application.Context.RootComponent.ShouldBeSameAs(rootComponent);
        application.Context.Components.ShouldBeSameAs(InlineComponentFactory.Instance);
        application.Context.Services.ShouldBeSameAs(services);
        application.Context.WarnHandler.ShouldBeSameAs(warnHandler);
    }

    [Fact]
    public async Task Builder_AttachesServices_ReachableFromSetupDuringRender()
    {
        Greeting greeting = new("hello");
        TestServiceProvider services = Services(greeting);
        InlineComponent component = new(context =>
        {
            Greeting resolved =
                (Greeting?)context.Services.GetService(typeof(Greeting))
                ?? throw new InvalidOperationException("Greeting was not supplied.");
            return () => TestTree.Element("div", resolved.Text);
        });
        ServerRenderApplication application = ServerRenderApplication
            .CreateBuilder(component.Request(), InlineComponentFactory.Instance, services)
            .Build();

        string html = await ServerRenderer.RenderToStringAsync(application);

        html.ShouldBe("<div>hello</div>");
        application.Context.Services.ShouldBeSameAs(services);
    }

    [Fact]
    public async Task ServerRenderApplication_DoesNotDisposeBorrowedServiceProvider()
    {
        TrackingServiceProvider services = new();
        InlineComponent component = new(_ => () => TestTree.Element("div", "x"));
        ServerRenderApplication application = new(
            component.Request(),
            InlineComponentFactory.Instance,
            services);

        await ServerRenderer.RenderToStringAsync(application);

        services.IsDisposed.ShouldBeFalse();
    }

    private static TestServiceProvider Services(Greeting greeting)
    {
        return new TestServiceProvider(
            new Dictionary<Type, object>
            {
                [typeof(Greeting)] = greeting,
            });
    }

    private sealed record Greeting(string Text);

    private sealed class TrackingServiceProvider : IServiceProvider, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public object? GetService(Type serviceType) => null;

        public void Dispose() => IsDisposed = true;
    }
}
