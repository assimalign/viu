using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins dynamic selection against Vue 3.5's replace-on-identity-change behavior.
/// </summary>
public sealed class DynamicComponentTests : IDisposable
{
    public DynamicComponentTests()
    {
        Scheduler.Reset();
    }

    public void Dispose()
    {
        Scheduler.Reset();
    }

    [Fact]
    public void DynamicComponent_SelectorKinds_CreateUnifiedTreeValuesWithoutActivation()
    {
        int activations = 0;
        AsynchronousComponentDefinition asynchronous =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                _ =>
                {
                    activations++;
                    return Task.FromResult(
                        AsynchronousComponentTarget.From<FirstTemplate>());
                });

        ITemplateComponent typed = DynamicComponents
            .DynamicComponent(typeof(FirstTemplate))
            .ShouldBeAssignableTo<ITemplateComponent>();
        ITemplateComponent named = DynamicComponents
            .DynamicComponent(DynamicComponents.Named("registered"))
            .ShouldBeAssignableTo<ITemplateComponent>();
        IElementComponent element = DynamicComponents
            .DynamicComponent("article")
            .ShouldBeAssignableTo<IElementComponent>();
        ITemplateComponent asynchronousRequest = DynamicComponents
            .DynamicComponent(asynchronous)
            .ShouldBeAssignableTo<ITemplateComponent>();
        ICommentComponent placeholder = DynamicComponents
            .DynamicComponent(null)
            .ShouldBeAssignableTo<ICommentComponent>();

        typed.TemplateType.ShouldBe(typeof(FirstTemplate));
        named.TemplateName.ShouldBe("registered");
        element.Tag.ShouldBe("article");
        asynchronousRequest.TemplateType.ShouldBe(typeof(AsynchronousIdentity));
        placeholder.Kind.ShouldBe(ComponentKind.Comment);
        activations.ShouldBe(0);
    }

    [Fact]
    public void ResolveDynamicComponent_PlainStringIsElement_ExplicitNameIsFactoryLookup()
    {
        DynamicComponents.ResolveDynamicComponent("registered")
            .ShouldBe("registered");
        DynamicComponentName named = DynamicComponents.Named("registered");
        DynamicComponents.ResolveDynamicComponent(named).ShouldBe(named);

        IElementComponent element = RenderHelpers
            ._createVNode(
                RenderHelpers._resolveDynamicComponent("registered"))
            .ShouldBeAssignableTo<IElementComponent>();
        ITemplateComponent template = RenderHelpers
            ._createVNode(
                RenderHelpers._resolveDynamicComponent(named))
            .ShouldBeAssignableTo<ITemplateComponent>();

        element.Tag.ShouldBe("registered");
        template.TemplateName.ShouldBe("registered");
    }

    [Fact]
    public void DynamicComponent_ReactiveTypeChange_UnmountsOldBeforeMountingNew()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        List<string> lifecycle = [];
        Reference<object?> selector =
            Reactive.Reference<object?>(typeof(FirstTemplate));
        ITemplateComponent root = ComponentTree.Template<DynamicHostTemplate>();
        ComponentFactory factory = new(
        [
            new ComponentRegistration(
                typeof(DynamicHostTemplate),
                () => new DynamicHostTemplate(selector)),
            new ComponentRegistration(
                typeof(FirstTemplate),
                () => new LifecycleTemplate("first", lifecycle)),
            new ComponentRegistration(
                typeof(SecondTemplate),
                () => new LifecycleTemplate("second", lifecycle)),
        ]);
        ApplicationContext application = new(
            root,
            factory,
            new EmptyServiceProvider());
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);
        lifecycle.ShouldBe(["first:mounted"]);
        host.Text(host.Root).ShouldBe("first");

        selector.Value = typeof(SecondTemplate);
        pump.RunUntilIdle();

        lifecycle.ShouldBe(
        [
            "first:mounted",
            "first:unmounted",
            "second:mounted",
        ]);
        host.Text(host.Root).ShouldBe("second");
    }

    private sealed class AsynchronousIdentity
    {
    }

    private sealed class FirstTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => ComponentTree.Text("first");
        }
    }

    private sealed class SecondTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => ComponentTree.Text("second");
        }
    }

    private sealed class DynamicHostTemplate : IComponentTemplate
    {
        private readonly Reference<object?> _selector;

        internal DynamicHostTemplate(Reference<object?> selector)
        {
            _selector = selector;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => DynamicComponents.DynamicComponent(_selector.Value);
        }
    }

    private sealed class LifecycleTemplate : IComponentTemplate
    {
        private readonly List<string> _lifecycle;
        private readonly string _name;

        internal LifecycleTemplate(
            string name,
            List<string> lifecycle)
        {
            _name = name;
            _lifecycle = lifecycle;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Lifecycle.OnMounted(
                () => _lifecycle.Add($"{_name}:mounted"));
            context.Lifecycle.OnUnmounted(
                () => _lifecycle.Add($"{_name}:unmounted"));
            return () => ComponentTree.Text(_name);
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
