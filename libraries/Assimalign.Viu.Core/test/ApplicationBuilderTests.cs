using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Tests;

// Pins the reshape's application builder ([V01.01.03.23]): the ApplicationBuilder base records
// plugins/provides/registrations and replays them onto the built application in call order, and the
// built application is mountable. Exercised through a concrete test builder over the in-memory
// renderer (Browser/ServerRenderer supply the real platform builders).
public sealed class ApplicationBuilderTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public ApplicationBuilderTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void Build_ProducesAMountableApplication_ThatRendersTheRoot()
    {
        var root = new TestComponent { SetupFunction = static (_, _) => static () => VirtualNodeFactory.Text("root") };
        var builder = new TestApplicationBuilder(_renderer.Renderer, root);

        var application = builder.Build();
        var instance = application.Mount(_container);

        application.IsMounted.ShouldBeTrue();
        instance.ShouldNotBeNull();
        instance.ShouldBeSameAs(application.RootInstance);
        TestNodeSerializer.Serialize(_container).ShouldContain("root");
    }

    [Fact]
    public void Build_ReplaysRecordedConfiguration_InCallOrder()
    {
        var order = new List<string>();
        var root = new TestComponent { SetupFunction = static (_, _) => static () => VirtualNodeFactory.Text("root") };
        var builder = new TestApplicationBuilder(_renderer.Renderer, root);

        // Interleave provides/plugins/config callbacks; every action must apply in the order recorded.
        builder.ConfigureApplication(_ => order.Add("config-a"));
        builder.Use(new RecordingPlugin(order, "plugin-a"));
        builder.Provide("key", "value");
        builder.Use(new RecordingPlugin(order, "plugin-b"));
        builder.ConfigureApplication(_ => order.Add("config-b"));

        order.ShouldBeEmpty(); // nothing runs until Build

        var application = builder.Build();

        order.ShouldBe(["config-a", "plugin-a", "plugin-b", "config-b"]);

        using var warnings = new WarningCapture();
        application.Provide("key", "again"); // re-providing the same key warns -> the builder's provide landed
        warnings.Messages.ShouldContain(message => message.Contains("already provides"));
    }

    [Fact]
    public void Build_AppliesPluginRegistrations_ToTheBuiltApplication()
    {
        var key = new InjectionKey<string>("plugin");
        var root = new TestComponent { SetupFunction = static (_, _) => static () => VirtualNodeFactory.Text("root") };
        var builder = new TestApplicationBuilder(_renderer.Renderer, root);
        builder.Use(new ProvidingPlugin(key, "installed"));

        var application = builder.Build();

        // The plugin registered a component and an app-level provide through the built app.
        application.Component("plugin-widget").ShouldNotBeNull();
        application.Context.Provides[key].ShouldBe("installed");
    }

    // A concrete builder over the in-memory renderer; Build creates the app and replays configuration.
    private sealed class TestApplicationBuilder(Renderer<TestNode> renderer, IComponentDefinition root, VirtualNodeProperties? properties = null)
        : ApplicationBuilder(root, properties)
    {
        public override Application<TestNode> Build()
        {
            var application = renderer.CreateApplication(RootComponent, RootProperties);
            ApplyConfiguration(application);
            return application;
        }
    }

    private sealed class RecordingPlugin(List<string> order, string name) : IPlugin
    {
        public void Install(IApplication application, object? options) => order.Add(name);
    }

    private sealed class ProvidingPlugin(InjectionKey<string> key, string value) : IPlugin
    {
        public void Install(IApplication application, object? options)
        {
            application.Component("plugin-widget", new TestComponent
            {
                SetupFunction = static (_, _) => static () => VirtualNodeFactory.Text("plugin-widget"),
            });
            application.Provide(key, value);
        }
    }
}
