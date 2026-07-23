using System;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>Pins the awaited server-prefetch lifecycle phase and its error/cancellation behavior.</summary>
public class ServerRendererServerPrefetchTests
{
    [Fact]
    public async Task ServerPrefetch_IsAwaitedBeforeSubtreeSerializes()
    {
        InlineComponent component = new(context =>
        {
            IReactiveReference<string> data = Reactive.Reference("loading");
            context.Lifecycle.OnServerPrefetch(async () =>
            {
                await Task.Yield();
                data.Value = "loaded";
            });
            return () => TestTree.Element("div", data.Value);
        });

        string html = await Ssr.RenderAsync(component);

        html.ShouldBe("<div>loaded</div>");
    }

    [Fact]
    public async Task ServerPrefetch_RunsExactlyOnce()
    {
        int prefetchCount = 0;
        InlineComponent component = new(context =>
        {
            context.Lifecycle.OnServerPrefetch(() =>
            {
                prefetchCount++;
                return Task.CompletedTask;
            });
            return () => TestTree.Element("div", "x");
        });

        await Ssr.RenderAsync(component);

        prefetchCount.ShouldBe(1);
    }

    [Fact]
    public async Task ServerPrefetch_Failure_WithNoHandler_FaultsTheRender()
    {
        InlineComponent component = new(context =>
        {
            context.Lifecycle.OnServerPrefetch(
                () => throw new InvalidOperationException("prefetch failed"));
            return () => TestTree.Element("div", "x");
        });

        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(
                () => Ssr.RenderAsync(component));

        exception.Message.ShouldBe("prefetch failed");
    }

    [Fact]
    public async Task ServerPrefetch_Failure_IsRoutedToApplicationErrorHandler()
    {
        Exception? captured = null;
        InlineComponent component = new(context =>
        {
            context.Lifecycle.OnServerPrefetch(
                () => throw new InvalidOperationException("boom"));
            return () => TestTree.Element("div", "recovered");
        });
        ServerApplication application = Ssr.Application(component.Request());
        application.Context.ErrorHandler = (exception, _, _) => captured = exception;

        string html = await ServerRenderer.RenderToStringAsync(application);

        captured.ShouldBeOfType<InvalidOperationException>();
        html.ShouldBe("<div>recovered</div>");
    }

    [Fact]
    public async Task DescendantPrefetchFailure_IsCapturedByAncestor()
    {
        Exception? captured = null;
        InlineComponent child = new(context =>
        {
            context.Lifecycle.OnServerPrefetch(
                () => throw new InvalidOperationException("child failure"));
            return () => TestTree.Element("span", "child");
        });
        InlineComponent parent = new(context =>
        {
            context.Lifecycle.OnErrorCaptured((exception, _, _) =>
            {
                captured = exception;
                return false;
            });
            return () => ComponentTree.Element(
                "div",
                children: [child.Request()]);
        });

        string html = await Ssr.RenderAsync(parent);

        captured.ShouldBeOfType<InvalidOperationException>();
        html.ShouldBe("<div><span>child</span></div>");
    }

    [Fact]
    public async Task RenderCancellation_CancelsComponentLifetimeToken()
    {
        TaskCompletionSource tokenCanceled =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource prefetchStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        InlineComponent component = new(context =>
        {
            context.Lifecycle.OnServerPrefetch(async cancellationToken =>
            {
                prefetchStarted.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    tokenCanceled.SetResult();
                    throw;
                }
            });
            return () => ComponentTree.Comment();
        });
        using CancellationTokenSource cancellation = new();
        Task<string> render = ServerRenderer.RenderToStringAsync(
            Ssr.Application(component.Request()),
            cancellationToken: cancellation.Token);

        await prefetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => render);
        await tokenCanceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
