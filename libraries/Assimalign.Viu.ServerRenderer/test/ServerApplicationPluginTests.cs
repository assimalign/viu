using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Assimalign.Viu;
using Assimalign.Viu.ServerRenderer;

namespace Assimalign.Viu.ServerRenderer.Tests;

public sealed class ServerApplicationPluginTests
{
    [Fact]
    public void Use_RepeatedPluginInstance_InstallsOnce_AndWarns()
    {
        var previousSink = RuntimeWarnings.Sink;
        var messages = new List<string>();
        RuntimeWarnings.Sink = messages.Add;
        try
        {
            var plugin = new CountingPlugin();
            var application = ServerApplication.CreateBuilder(new NullComponent()).Build();

            application.Use(plugin).Use(plugin);

            // Mirrors Application<TNode>.Use ([V01.01.03.27]): install exactly once per instance,
            // and the repeat registration surfaces a dev warning instead of passing silently.
            plugin.InstallCount.ShouldBe(1);
            messages.ShouldContain(message => message.Contains("already been applied"));
        }
        finally
        {
            RuntimeWarnings.Sink = previousSink;
        }
    }

    private sealed class CountingPlugin : IApplicationPlugin
    {
        public int InstallCount { get; private set; }

        public ValueTask InstallAsync(IApplication application)
        {
            InstallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NullComponent : IComponent
    {
        public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context) => () => null;
    }
}
