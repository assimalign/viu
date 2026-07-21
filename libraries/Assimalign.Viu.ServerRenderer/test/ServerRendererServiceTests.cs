using System;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer.Tests;

// Pins the server application's service surface ([V01.01.03.24]): the builder attaches an
// IServiceProvider (IApplication.Services) reachable from Setup during render, providers are isolated
// per request (per app), and Dispose cascades to owned disposable singletons.
public class ServerRendererServiceTests
{
    private sealed class Greeting
    {
        public Greeting(string text) => Text = text;

        public string Text { get; }
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    [Fact]
    public async Task Builder_AttachesServices_ReachableFromSetupDuringRender()
    {
        var service = new Greeting("hello");
        var component = new InlineComponent((_, _) =>
        {
            var greeting = DependencyInjection.GetRequiredService<Greeting>();
            return () => VirtualNodeFactory.Element("div", greeting.Text);
        });
        var builder = ServerApplication.CreateBuilder(component);
        builder.Services.AddSingleton(service);
        var application = builder.Build();

        var html = await ServerRenderer.RenderToStringAsync(application);

        html.ShouldBe("<div>hello</div>");
        application.Services.ShouldNotBeNull();
        application.Services!.GetRequiredService<Greeting>().ShouldBeSameAs(service);
    }

    [Fact]
    public void TwoServerApplications_HaveIsolatedServiceSingletons()
    {
        ServerApplication BuildApp()
        {
            var builder = ServerApplication.CreateBuilder(
                new InlineComponent((_, _) => static () => VirtualNodeFactory.Element("div", "x")));
            builder.Services.AddSingleton(_ => new Greeting("g"));
            return builder.Build();
        }

        var appA = BuildApp();
        var appB = BuildApp();

        appA.Services!.GetRequiredService<Greeting>()
            .ShouldNotBeSameAs(appB.Services!.GetRequiredService<Greeting>());
    }

    [Fact]
    public void Dispose_DisposesOwnedDisposableSingleton()
    {
        var disposable = new TrackingDisposable();
        var builder = ServerApplication.CreateBuilder(
            new InlineComponent((_, _) => static () => VirtualNodeFactory.Element("div", "x")));
        builder.Services.AddSingleton(disposable);
        var application = builder.Build();
        _ = application.Services!.GetRequiredService<TrackingDisposable>(); // realize the singleton

        application.Dispose();

        disposable.IsDisposed.ShouldBeTrue();
    }
}
