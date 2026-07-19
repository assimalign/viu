using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Testing;

namespace Assimalign.Viu.RuntimeCore.Tests;

// Pins the minimal application shell of @vue/runtime-core's apiCreateApp.ts —
// https://vuejs.org/api/application.html (plugins/config land with [V01.01.03.12]).
public class ViuApplicationTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public ViuApplicationTests()
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

    private static TestComponent Root(string text = "app") => new()
    {
        SetupFunction = (_, _) => () => VirtualNodeFactory.Element("div", text),
    };

    [Fact]
    public void Mount_RendersTheRootComponent_AndReturnsItsInstance()
    {
        var application = _renderer.Renderer.CreateApplication(Root());

        var instance = application.Mount(_container);

        application.IsMounted.ShouldBeTrue();
        instance.ShouldNotBeNull();
        instance.ShouldBeSameAs(application.RootInstance);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>app</div></root>");
    }

    [Fact]
    public void SecondMount_WarnsAndNoOps_ReturningTheExistingInstance()
    {
        using var warnings = new WarningCapture();
        var application = _renderer.Renderer.CreateApplication(Root());
        var first = application.Mount(_container);

        var second = application.Mount(_renderer.CreateContainer());

        warnings.Messages.ShouldContain(message => message.Contains("already been mounted"));
        second.ShouldBeSameAs(first);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>app</div></root>");
    }

    [Fact]
    public void Unmount_RunsTeardownLifecycles_AndClearsTheContainer()
    {
        var unmounted = false;
        var root = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnUnmounted(() => unmounted = true);
                return static () => VirtualNodeFactory.Element("div", "app");
            },
        };
        var application = _renderer.Renderer.CreateApplication(root);
        application.Mount(_container);

        application.Unmount();

        application.IsMounted.ShouldBeFalse();
        unmounted.ShouldBeTrue();
        _container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void RootProperties_FlowToTheRootComponent()
    {
        string? seen = null;
        var root = new TestComponent
        {
            Properties = [new ComponentPropertyDefinition("greeting")],
            SetupFunction = (properties, _) => () =>
            {
                seen = (string?)properties["greeting"];
                return VirtualNodeFactory.Element("div", seen ?? string.Empty);
            },
        };
        var application = _renderer.Renderer.CreateApplication(
            root, VirtualNodeFactory.Properties(("greeting", "hello")));

        application.Mount(_container);

        seen.ShouldBe("hello");
    }
}
