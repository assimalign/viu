using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu.Reactivity;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>Pins per-application service isolation and fresh template state per render.</summary>
public class ServerRendererIsolationTests
{
    [Fact]
    public async Task TwoApplications_WithDistinctServices_RenderIndependently()
    {
        InlineComponent component = new(context =>
        {
            Tenant tenant =
                (Tenant?)context.Services.GetService(typeof(Tenant))
                ?? throw new InvalidOperationException("Tenant was not supplied.");
            return () => TestTree.Element("div", tenant.Name);
        });
        ServerApplication applicationA = Ssr.Application(
            component.Request(),
            Services(new Tenant("tenant-a")));
        ServerApplication applicationB = Ssr.Application(
            component.Request(),
            Services(new Tenant("tenant-b")));

        string htmlA = await ServerRenderer.RenderToStringAsync(applicationA);
        string htmlB = await ServerRenderer.RenderToStringAsync(applicationB);
        string htmlARepeat = await ServerRenderer.RenderToStringAsync(applicationA);

        htmlA.ShouldBe("<div>tenant-a</div>");
        htmlB.ShouldBe("<div>tenant-b</div>");
        htmlARepeat.ShouldBe("<div>tenant-a</div>");
    }

    [Fact]
    public async Task ConcurrentRenders_InterleavingAtAsyncBoundaries_KeepStateIsolated()
    {
        InlineComponent Make(string value) => new(context =>
        {
            IReactiveReference<string> data = Reactive.Reference("pending");
            context.Lifecycle.OnServerPrefetch(async () =>
            {
                await Task.Yield();
                data.Value = value;
            });
            return () => TestTree.Element("div", data.Value);
        });

        Task<string> renderA = Ssr.RenderAsync(Make("A"));
        Task<string> renderB = Ssr.RenderAsync(Make("B"));
        string[] results = await Task.WhenAll(renderA, renderB);

        results[0].ShouldBe("<div>A</div>");
        results[1].ShouldBe("<div>B</div>");
    }

    [Fact]
    public async Task RepeatedRenderOfSameDefinition_HasIndependentSetupState()
    {
        InlineComponent component = new(_ =>
        {
            IReactiveReference<int> count = Reactive.Reference(0);
            count.Value++;
            return () => TestTree.Element("div", count.Value.ToString());
        });
        ServerApplication application = Ssr.Application(component.Request());

        string first = await ServerRenderer.RenderToStringAsync(application);
        string second = await ServerRenderer.RenderToStringAsync(application);

        first.ShouldBe("<div>1</div>");
        second.ShouldBe("<div>1</div>");
    }

    private static TestServiceProvider Services(Tenant tenant)
    {
        return new TestServiceProvider(
            new Dictionary<Type, object>
            {
                [typeof(Tenant)] = tenant,
            });
    }

    private sealed record Tenant(string Name);
}
