using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Per-request app-instance discipline: two renders with distinct <see cref="ServerApplication"/>
/// instances share no reactive state, even when they interleave at async boundaries — the
/// cross-request-isolation property server rendering requires (Vue's app-per-request rule).
/// </summary>
public class ServerRendererIsolationTests
{
    private static InlineComponent CounterComponent()
        => new((_, _) =>
        {
            // Fresh reactive state per instance — the isolation unit. Two renders each get their own.
            var count = Reactive.Reference(0);
            count.Value = 1;
            return () => VirtualNodeFactory.Element("div", count.Value.ToString());
        });

    [Fact]
    public async Task TwoApplications_WithDistinctProvides_RenderIndependently()
    {
        var key = new InjectionKey<string>("tenant");
        InlineComponent Make() => new((_, _) =>
        {
            var tenant = DependencyInjection.Inject(key, "none");
            return () => VirtualNodeFactory.Element("div", tenant!);
        });

        var applicationA = new ServerApplication(Make()).Provide(key, "tenant-a");
        var applicationB = new ServerApplication(Make()).Provide(key, "tenant-b");

        var htmlA = await ServerRenderer.RenderToStringAsync(applicationA);
        var htmlB = await ServerRenderer.RenderToStringAsync(applicationB);
        // Re-render A after B to prove B did not leak into A's context.
        var htmlARepeat = await ServerRenderer.RenderToStringAsync(applicationA);

        htmlA.ShouldBe("<div>tenant-a</div>");
        htmlB.ShouldBe("<div>tenant-b</div>");
        htmlARepeat.ShouldBe("<div>tenant-a</div>");
    }

    [Fact]
    public async Task ConcurrentRenders_InterleavingAtAsyncBoundaries_DoNotPolluteEachOther()
    {
        // Each app's component awaits a prefetch (forcing the two renders to interleave), then reads its
        // own reactive state. A shared-state bug would surface as one render observing the other's value.
        InlineComponent Make(string value) => new((_, _) =>
        {
            var data = Reactive.Reference("pending");
            Lifecycle.OnServerPrefetch(async () =>
            {
                await Task.Yield();
                data.Value = value;
            });
            return () => VirtualNodeFactory.Element("div", data.Value);
        });

        var renderA = ServerRenderer.RenderToStringAsync(new ServerApplication(Make("A")));
        var renderB = ServerRenderer.RenderToStringAsync(new ServerApplication(Make("B")));
        var results = await Task.WhenAll(renderA, renderB);

        results[0].ShouldBe("<div>A</div>");
        results[1].ShouldBe("<div>B</div>");
    }

    [Fact]
    public async Task RepeatedRenderOfSameComponentDefinition_HasIndependentState()
    {
        // The same definition rendered twice builds two instances with independent state (no module-level
        // singleton holding the counter).
        var definition = CounterComponent();

        var first = await ServerRenderer.RenderToStringAsync(definition);
        var second = await ServerRenderer.RenderToStringAsync(definition);

        first.ShouldBe("<div>1</div>");
        second.ShouldBe("<div>1</div>");
    }
}
