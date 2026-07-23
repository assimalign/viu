using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

public sealed class ServerApplicationPluginTests
{
    [Fact]
    public async Task Use_RepeatedPluginInstance_InstallsOnce_AndWarns()
    {
        List<string> messages = [];
        CountingPlugin plugin = new();
        ServerApplication application = Ssr.Application(ComponentTree.Comment());
        application.Context.WarnHandler = messages.Add;

        application.Use(plugin).Use(plugin);
        string html = await ServerRenderer.RenderToStringAsync(application);

        html.ShouldBe("<!---->");
        plugin.InstallCount.ShouldBe(1);
        messages.ShouldContain(message => message.Contains("already been applied"));
    }

    [Fact]
    public async Task Render_AwaitsAsynchronousPluginInstallation()
    {
        AsyncPlugin plugin = new();
        ServerApplication application = Ssr.Application(ComponentTree.Comment());
        application.Use(plugin);

        Task<string> render = ServerRenderer.RenderToStringAsync(application);
        render.IsCompleted.ShouldBeFalse();

        plugin.Complete();
        (await render).ShouldBe("<!---->");
    }

    private sealed class CountingPlugin : IApplicationPlugin
    {
        public int InstallCount { get; private set; }

        public ValueTask InstallAsync(
            IApplication application,
            CancellationToken cancellationToken = default)
        {
            InstallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AsyncPlugin : IApplicationPlugin
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask InstallAsync(
            IApplication application,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask(_completion.Task.WaitAsync(cancellationToken));
        }

        internal void Complete() => _completion.SetResult();
    }
}
