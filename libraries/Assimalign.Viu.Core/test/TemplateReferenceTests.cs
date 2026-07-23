using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

public sealed class TemplateReferenceTests : IDisposable
{
    private readonly TestSchedulerPump _pump;

    public TemplateReferenceTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
    }

    public void Dispose()
    {
        _pump.Dispose();
        Scheduler.Reset();
    }

    [Fact]
    public void Render_ElementReference_AssignsHostNodeAndClearsOnUnmount()
    {
        Reference<object?> value = Reactive.Reference<object?>(null);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(
            ComponentTree.Element(
                "div",
                reference: TemplateReference.FromReference(value)),
            host.Root);

        value.Value.ShouldBeSameAs(host.Root.Children.Single());

        renderer.Render(null, host.Root);

        value.Value.ShouldBeNull();
    }

    [Fact]
    public void Render_ChangedCallbackReference_ClearsOldBeforeAssigningNew()
    {
        List<string> calls = [];
        IComponentReference first = TemplateReference.FromCallback(
            value => calls.Add(value is null ? "first:null" : "first:value"));
        IComponentReference second = TemplateReference.FromCallback(
            value => calls.Add(value is null ? "second:null" : "second:value"));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(
            ComponentTree.Element("div", reference: first),
            host.Root);
        renderer.Render(
            ComponentTree.Element("div", reference: second),
            host.Root);

        calls.ShouldBe(
        [
            "first:value",
            "first:null",
            "second:value",
        ]);
    }

    [Fact]
    public void Render_TemplateReference_ReceivesExplicitExposedSurface()
    {
        object exposed = new();
        Reference<object?> value = Reactive.Reference<object?>(null);
        ExposingTemplate template = new(exposed);
        ITemplateComponent root = ComponentTree.Template<ExposingTemplate>(
            reference: TemplateReference.FromReference(value));
        IApplicationContext application = CreateApplication(root, template);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);

        value.Value.ShouldBeSameAs(exposed);
    }

    [Fact]
    public void Render_TemplateReferenceWithoutExpose_ReceivesComponentContext()
    {
        Reference<object?> value = Reactive.Reference<object?>(null);
        PlainTemplate template = new();
        ITemplateComponent root = ComponentTree.Template<PlainTemplate>(
            reference: TemplateReference.FromReference(value));
        IApplicationContext application = CreateApplication(root, template);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        IComponentContext context = renderer.Render(
            root,
            host.Root,
            application)!;

        value.Value.ShouldBeSameAs(context);
    }

    [Fact]
    public void Render_ElementReference_IsAssignedBeforeMountedLifecycle()
    {
        Reference<object?> value = Reactive.Reference<object?>(null);
        ObservingTemplate template = new(value);
        ITemplateComponent root = ComponentTree.Template<ObservingTemplate>();
        IApplicationContext application = CreateApplication(root, template);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);

        template.Observed.ShouldBeSameAs(value.Value);
        template.Observed.ShouldBeSameAs(host.Root.Children.Single());
    }

    [Fact]
    public void RenderHelpers_ReferenceProperty_BecomesMetadataNotHostAttribute()
    {
        Reference<object?> value = Reactive.Reference<object?>(null);
        IElementComponent component = RenderHelpers._createElementVNode(
            "input",
            new Dictionary<string, object?>
            {
                ["ref"] = value,
                ["type"] = "text",
            }).ShouldBeOfType<ElementComponent>();

        component.Reference.ShouldNotBeNull();
        component.Attributes.TryGetValue("ref", out _).ShouldBeFalse();
        component.Attributes.TryGetValue("type", out object? type).ShouldBeTrue();
        type.ShouldBe("text");
    }

    [Fact]
    public void RenderHelpers_InvalidReferenceProperty_WarnsThroughCurrentApplication()
    {
        List<string> warnings = [];
        InvalidGeneratedReferenceTemplate template = new();
        ITemplateComponent root =
            ComponentTree.Template<InvalidGeneratedReferenceTemplate>();
        IApplicationContext application = CreateApplication(root, template);
        application.WarnHandler = warnings.Add;
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);

        warnings.ShouldContain(
            message => message.Contains(
                "Invalid template reference",
                StringComparison.Ordinal));
        host.Root.Children.Single().Attributes.ShouldNotContainKey("ref");
    }

    [Fact]
    public void Render_CallbackReferenceThrowingOnUnmount_RoutesThroughOwnerErrorCapture()
    {
        ReferenceErrorParentTemplate parent = new();
        ReferenceErrorChildTemplate child = new();
        ITemplateComponent root =
            ComponentTree.Template<ReferenceErrorParentTemplate>();
        IApplicationContext application =
            CreateApplication(root, parent, child);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);

        Should.NotThrow(() => renderer.Render(null, host.Root));
        parent.CapturedException.ShouldNotBeNull();
        parent.CapturedException.Message.ShouldBe("reference teardown failed");
        parent.CapturedInformation.ShouldBe("template reference callback");
        host.Root.Children.ShouldBeEmpty();
    }

    private static IApplicationContext CreateApplication(
        ITemplateComponent root,
        params IComponentTemplate[] templates)
    {
        ComponentRegistration[] registrations =
            new ComponentRegistration[templates.Length];
        for (int index = 0; index < templates.Length; index++)
        {
            IComponentTemplate template = templates[index];
            registrations[index] = new ComponentRegistration(
                template.GetType(),
                () => template);
        }

        return new ApplicationContext(
            root,
            new ComponentFactory(registrations),
            new EmptyServiceProvider());
    }

    private sealed class ExposingTemplate : IComponentTemplate
    {
        private readonly object _exposed;

        internal ExposingTemplate(object exposed)
        {
            _exposed = exposed;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Expose(_exposed);
            return static () => ComponentTree.Element("div");
        }
    }

    private sealed class PlainTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => ComponentTree.Element("div");
        }
    }

    private sealed class ObservingTemplate : IComponentTemplate
    {
        private readonly Reference<object?> _reference;

        internal ObservingTemplate(Reference<object?> reference)
        {
            _reference = reference;
        }

        internal object? Observed { get; private set; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Lifecycle.OnMounted(() => Observed = _reference.Value);
            return () => ComponentTree.Element(
                "div",
                reference: TemplateReference.FromReference(_reference));
        }
    }

    private sealed class InvalidGeneratedReferenceTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => RenderHelpers._createElementVNode(
                "div",
                new Dictionary<string, object?>
                {
                    ["ref"] = "string-reference",
                });
        }
    }

    private sealed class ReferenceErrorParentTemplate : IComponentTemplate
    {
        internal Exception? CapturedException { get; private set; }

        internal string? CapturedInformation { get; private set; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Lifecycle.OnErrorCaptured(
                (exception, _, information) =>
                {
                    CapturedException = exception;
                    CapturedInformation = information;
                    return false;
                });
            return static () =>
                ComponentTree.Template<ReferenceErrorChildTemplate>();
        }
    }

    private sealed class ReferenceErrorChildTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => ComponentTree.Element(
                "div",
                reference: TemplateReference.FromCallback(
                    value =>
                    {
                        if (value is null)
                        {
                            throw new InvalidOperationException(
                                "reference teardown failed");
                        }
                    }));
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
