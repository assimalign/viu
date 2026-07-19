using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Testing;

namespace Assimalign.Viu.RuntimeCore.Tests;

// Pins the application API of @vue/runtime-core's apiCreateApp.ts — component/directive registries,
// app-level provide (final inject fallback), plugins, and config error/warn handlers —
// https://vuejs.org/api/application.html. Exercised through the in-memory renderer.
public class ApplicationApiTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public ApplicationApiTests()
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

    private static TestComponent Leaf(string text = "leaf", Action<ComponentProperties, ComponentSetupContext>? onSetup = null)
        => new()
        {
            SetupFunction = (properties, context) =>
            {
                onSetup?.Invoke(properties, context);
                return () => VirtualNodeFactory.Text(text);
            },
        };

    [Fact]
    public void RegisteredComponent_ResolvesDuringRender_ForDescendantsOfTheRoot()
    {
        var widget = Leaf("widget");
        IComponentDefinition? resolvedInChild = null;

        var child = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                // A descendant resolves the app registry during its own render window.
                resolvedInChild = ComponentInstance.Current!.AppContext!.ResolveComponent("my-widget");
                return static () => VirtualNodeFactory.Text("child");
            },
        };
        var root = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Component(child),
        };
        var application = _renderer.Renderer.CreateApplication(root);
        application.Component("my-widget", widget);

        application.Mount(_container);

        resolvedInChild.ShouldBeSameAs(widget);
        // Case-insensitive resolution: my-widget also resolves a MyWidget registration and vice versa.
        _renderer.Renderer.CreateApplication(root).Component("MyWidget", widget).Context
            .ResolveComponent("my-widget").ShouldBeSameAs(widget);
    }

    [Fact]
    public void ComponentGetter_ReturnsRegisteredDefinition_OrNull()
    {
        var widget = Leaf();
        var application = _renderer.Renderer.CreateApplication(Leaf());

        application.Component("widget", widget).ShouldBeSameAs(application); // chainable

        application.Component("widget").ShouldBeSameAs(widget);
        application.Component("missing").ShouldBeNull();
    }

    [Fact]
    public void AppProvide_IsInjectableAppWide_AsTheFinalFallback()
    {
        var key = new InjectionKey<string>("theme");
        string? injectedAtDepth = null;

        var leaf = Leaf(onSetup: (_, _) => injectedAtDepth = DependencyInjection.Inject(key));
        var middle = new TestComponent { SetupFunction = (_, _) => () => VirtualNodeFactory.Component(leaf) };
        var root = new TestComponent { SetupFunction = (_, _) => () => VirtualNodeFactory.Component(middle) };

        var application = _renderer.Renderer.CreateApplication(root);
        application.Provide(key, "dark");

        application.Mount(_container);

        injectedAtDepth.ShouldBe("dark");
    }

    [Fact]
    public void ComponentProvider_ShadowsTheAppProvide()
    {
        var key = new InjectionKey<string>("k");
        string? injected = null;

        var leaf = Leaf(onSetup: (_, _) => injected = DependencyInjection.Inject(key));
        var provider = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                DependencyInjection.Provide(key, "component");
                return () => VirtualNodeFactory.Component(leaf);
            },
        };
        var application = _renderer.Renderer.CreateApplication(provider);
        application.Provide(key, "app");

        application.Mount(_container);

        // A nearer component provider wins; the app value is only the last resort.
        injected.ShouldBe("component");
    }

    [Fact]
    public void Use_InvokesInstallExactlyOncePerInstance_AndDedupesOnRepeat()
    {
        using var warnings = new WarningCapture();
        var key = new InjectionKey<string>("plugin");
        var plugin = new RecordingPlugin(key);
        string? injected = null;

        var leaf = Leaf(onSetup: (_, _) => injected = DependencyInjection.Inject(key));
        var application = _renderer.Renderer.CreateApplication(
            new TestComponent { SetupFunction = (_, _) => () => VirtualNodeFactory.Component(leaf) });

        application.Use(plugin).Use(plugin); // second Use is a no-op

        plugin.InstallCount.ShouldBe(1);
        warnings.Messages.ShouldContain(message => message.Contains("already been applied"));

        application.Mount(_container);

        // The plugin registered a component and an app-level provide through the app.
        application.Component("plugin-widget").ShouldNotBeNull();
        injected.ShouldBe("from-plugin");
    }

    [Fact]
    public void ConfigErrorHandler_ReceivesRenderErrors_WithoutRethrowing()
    {
        Exception? seen = null;
        ComponentInstance? seenInstance = null;
        string? seenInfo = null;

        var failing = new TestComponent
        {
            Name = "Boom",
            SetupFunction = static (_, _) => static () => throw new InvalidOperationException("render boom"),
        };
        var application = _renderer.Renderer.CreateApplication(failing);
        application.Config.ErrorHandler = (exception, instance, info) =>
        {
            seen = exception;
            seenInstance = instance;
            seenInfo = info;
        };

        Should.NotThrow(() => { application.Mount(_container); });

        seen.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("render boom");
        seenInstance.ShouldBeSameAs(application.RootInstance);
        seenInfo.ShouldNotBeNull();
        seenInfo.ShouldContain("render");
    }

    [Fact]
    public void ConfigErrorHandler_Unset_LetsUnhandledErrorsRethrow()
    {
        var failing = new TestComponent
        {
            SetupFunction = static (_, _) => static () => throw new InvalidOperationException("unhandled"),
        };
        var application = _renderer.Renderer.CreateApplication(failing);

        var thrown = Record.Exception(() => { application.Mount(_container); });

        thrown.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("unhandled");
    }

    [Fact]
    public void ConfigWarnHandler_InterceptsDevWarnings_WhileMounted()
    {
        var captured = new List<string>();
        var key = new InjectionKey<string>("absent");

        var root = Leaf(onSetup: (_, _) => DependencyInjection.Inject(key)); // warns: injection not found
        var application = _renderer.Renderer.CreateApplication(root);
        application.Config.WarnHandler = captured.Add;

        application.Mount(_container);

        captured.ShouldContain(message => message.Contains("not found"));

        application.Unmount(); // restores the previous sink
    }

    [Fact]
    public void RegisteringAfterMount_WarnsInDev()
    {
        using var warnings = new WarningCapture();
        var application = _renderer.Renderer.CreateApplication(Leaf());
        application.Mount(_container);

        application.Component("late", Leaf());
        application.Provide("late-key", "value");

        warnings.Messages.Count(message => message.Contains("already mounted")).ShouldBe(2);
    }

    // A plugin that registers a component and an app-level provide, counting its installs.
    private sealed class RecordingPlugin(InjectionKey<string> key) : IPlugin<TestNode>
    {
        public int InstallCount { get; private set; }

        public void Install(Application<TestNode> application, object? options)
        {
            InstallCount++;
            application.Component("plugin-widget", new TestComponent
            {
                SetupFunction = static (_, _) => static () => VirtualNodeFactory.Text("plugin-widget"),
            });
            application.Provide(key, "from-plugin");
        }
    }
}
