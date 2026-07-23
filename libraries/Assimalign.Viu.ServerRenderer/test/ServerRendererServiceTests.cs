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
        ServerApplication application = ServerApplication
            .CreateBuilder(component.Request(), InlineComponentFactory.Instance, services)
            .Build();

        string html = await ServerRenderer.RenderToStringAsync(application);

        html.ShouldBe("<div>hello</div>");
        application.Context.Services.ShouldBeSameAs(services);
    }

    [Fact]
    public async Task ServerApplication_DoesNotDisposeBorrowedServiceProvider()
    {
        TrackingServiceProvider services = new();
        InlineComponent component = new(_ => () => TestTree.Element("div", "x"));
        ServerApplication application = new(
            component.Request(),
            InlineComponentFactory.Instance,
            services);

        await ServerRenderer.RenderToStringAsync(application);
        application.Unmount();
        await application.UnmountAsync();

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
