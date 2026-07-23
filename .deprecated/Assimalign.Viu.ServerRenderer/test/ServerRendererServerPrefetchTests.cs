using System;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// <see cref="Lifecycle.OnServerPrefetch"/> is awaited before a component's subtree serializes, runs
/// once, and routes failures through the SSR error path — pinned to upstream
/// <c>renderComponentSubTree</c>'s prefetch await.
/// </summary>
public class ServerRendererServerPrefetchTests
{
    [Fact]
    public async Task ServerPrefetch_IsAwaitedBeforeSubtreeSerializes()
    {
        // The render reads a value the prefetch sets asynchronously; seeing "loaded" proves the prefetch
        // completed before the subtree rendered.
        var component = new InlineComponent((_, _) =>
        {
            var data = Reactive.Reference("loading");
            Lifecycle.OnServerPrefetch(async () =>
            {
                await Task.Yield();
                data.Value = "loaded";
            });
            return () => VirtualNodeFactory.Element("div", data.Value);
        });

        var html = await Ssr.RenderAsync(component);

        html.ShouldBe("<div>loaded</div>");
    }

    [Fact]
    public async Task ServerPrefetch_RunsExactlyOnce()
    {
        var prefetchCount = 0;
        var component = new InlineComponent((_, _) =>
        {
            Lifecycle.OnServerPrefetch(() =>
            {
                prefetchCount++;
                return Task.CompletedTask;
            });
            return () => VirtualNodeFactory.Element("div", "x");
        });

        await Ssr.RenderAsync(component);

        prefetchCount.ShouldBe(1);
    }

    [Fact]
    public async Task ServerPrefetch_Failure_WithNoHandler_FaultsTheRender()
    {
        var component = new InlineComponent((_, _) =>
        {
            Lifecycle.OnServerPrefetch(() => throw new InvalidOperationException("prefetch failed"));
            return () => VirtualNodeFactory.Element("div", "x");
        });

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => Ssr.RenderAsync(component));
        exception.Message.ShouldBe("prefetch failed");
    }

    [Fact]
    public async Task ServerPrefetch_Failure_IsRoutedToAppErrorHandler()
    {
        Exception? captured = null;
        var component = new InlineComponent((_, _) =>
        {
            Lifecycle.OnServerPrefetch(() => throw new InvalidOperationException("boom"));
            return () => VirtualNodeFactory.Element("div", "recovered");
        });
        var application = new ServerApplication(component);
        application.Context.ErrorHandler = (exception, _, _) => captured = exception;

        var html = await ServerRenderer.RenderToStringAsync(application);

        // The app-level handler consumed the error, so the render completed.
        captured.ShouldBeOfType<InvalidOperationException>();
        html.ShouldBe("<div>recovered</div>");
    }
}
