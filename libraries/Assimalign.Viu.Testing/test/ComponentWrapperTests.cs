using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Tests;

public sealed class ComponentWrapperTests
{
    [Fact]
    public void Mount_SuppliedInstance_QueriesRenderedHostRange()
    {
        using ComponentWrapper wrapper = ViuTest.Mount(new RootComponent());

        wrapper.Exists().ShouldBeTrue();
        wrapper.Instance.ShouldBeOfType<RootComponent>();
        wrapper.Context.ShouldNotBeNull();
        wrapper.Html().ShouldBe(
            "<main id=\"root\" class=\"shell\"><span>hello</span></main>");
        wrapper.Text().ShouldBe("hello");
        wrapper.Get("#root").Attribute("class").ShouldBe("shell");
        wrapper.FindAll("span").Count.ShouldBe(1);
    }

    [Fact]
    public void FindComponent_TypeAndAncestry_ScopesChildHostRange()
    {
        ComponentFactory descendants = new();
        ComponentReference childReference = ComponentReference.ForType(typeof(ChildComponent));
        descendants.Register(
            new ComponentRegistration(
                childReference,
                new ComponentContract(displayName: "Child"),
                _ => new ChildComponent()));
        ComponentMountOptions options = new()
        {
            Components = descendants,
        };
        using ComponentWrapper root = ViuTest.Mount(
            new ParentComponent(childReference),
            options);

        ComponentWrapper child = root.GetComponent<ChildComponent>();

        child.Instance.ShouldBeOfType<ChildComponent>();
        child.Context.ShouldNotBeNull().Parent.ShouldBeSameAs(root.Context);
        child.Html().ShouldBe("<section>child</section>");
        child.Text().ShouldBe("child");
        root.Html().ShouldBe("<main><section>child</section><footer>tail</footer></main>");
    }

    [Fact]
    public void ChildWrapper_RootUnmount_StableViewIdentityReportsNotMounted()
    {
        ComponentFactory descendants = CreateChildFactory(out ComponentReference childReference);
        using ComponentWrapper root = ViuTest.Mount(
            new ParentComponent(childReference),
            new ComponentMountOptions { Components = descendants });
        ComponentWrapper child = root.GetComponent<ChildComponent>();
        child.Exists().ShouldBeTrue();

        root.Unmount();

        root.Exists().ShouldBeFalse();
        child.Exists().ShouldBeFalse();
        child.Html().ShouldBe(string.Empty);
    }

    [Fact]
    public void EventObserver_RootAndChildEmissions_AreCapturedPerContext()
    {
        ComponentReference childReference = ComponentReference.ForType(
            typeof(EmittingChildComponent));
        ComponentFactory descendants = new();
        descendants.Register(
            new ComponentRegistration(
                childReference,
                new ComponentContract(
                    displayName: "EmittingChild",
                    events: new[] { new ComponentEvent("child-ready") }),
                _ => new EmittingChildComponent()));
        ComponentMountOptions options = new()
        {
            Components = descendants,
            RootContract = new ComponentContract(
                displayName: "EmittingRoot",
                events: new[] { new ComponentEvent("root-ready") }),
        };
        using ComponentWrapper root = ViuTest.Mount(
            new EmittingRootComponent(childReference),
            options);
        ComponentWrapper child = root.GetComponent<EmittingChildComponent>();

        root.Emitted("root-ready").ShouldHaveSingleItem()[0].ShouldBe("root");
        root.Emitted("child-ready").ShouldBeEmpty();
        child.Emitted("child-ready").ShouldHaveSingleItem()[0].ShouldBe("child");
        child.Emitted("root-ready").ShouldBeEmpty();
    }

    [Fact]
    public void EventObserver_ConfiguredObserver_IsPreservedAlongsideCapture()
    {
        int configuredObservations = 0;
        ComponentMountOptions options = new()
        {
            RootContract = new ComponentContract(
                displayName: "Observed",
                events: new[] { new ComponentEvent("ready") }),
            ConfigureApplication = application =>
            {
                application.EventObserver = (_, name, _) =>
                {
                    if (name == "ready")
                    {
                        configuredObservations++;
                    }
                };
            },
        };

        using ComponentWrapper wrapper = ViuTest.Mount(new ObservedComponent(), options);

        configuredObservations.ShouldBe(1);
        wrapper.Emitted("ready").Count.ShouldBe(1);
    }

    [Fact]
    public void Stub_DescendantType_RendersGeneratedPlaceholder()
    {
        ComponentReference childReference = ComponentReference.ForType(typeof(ChildComponent));
        ComponentMountOptions options = new();
        options.Stub<ChildComponent>();

        using ComponentWrapper wrapper = ViuTest.Mount(
            new ParentComponent(childReference),
            options);

        wrapper.Html().ShouldContain("<child-component-stub>");
        wrapper.FindComponent<ChildComponent>().ShouldBeNull();
    }

    [Fact]
    public async Task Trigger_ReactiveListener_DrainsDeterministicScheduler()
    {
        using ComponentWrapper wrapper = ViuTest.Mount(new InteractiveComponent());
        wrapper.Text().ShouldBe("0");

        await wrapper.Trigger("click");

        wrapper.Text().ShouldBe("1");
    }

    [Fact]
    public void Mount_InvocationArguments_AreContractResolvedForSuppliedInstance()
    {
        ComponentMountOptions options = new()
        {
            RootContract = new ComponentContract(
                displayName: "Parameter",
                parameters: new[] { new ComponentParameter("message") }),
            Arguments = new Dictionary<string, object?>
            {
                ["message"] = "configured",
            },
        };

        using ComponentWrapper wrapper = ViuTest.Mount(new ParameterComponent(), options);

        wrapper.Text().ShouldBe("configured");
    }

    private static ComponentFactory CreateChildFactory(out ComponentReference childReference)
    {
        childReference = ComponentReference.ForType(typeof(ChildComponent));
        ComponentFactory descendants = new();
        descendants.Register(
            new ComponentRegistration(
                childReference,
                new ComponentContract(displayName: "Child"),
                _ => new ChildComponent()));
        return descendants;
    }

    private sealed class RootComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new ElementNode(
                new QualifiedName("main"),
                bindings:
                [
                    ElementBinding.Attribute(new QualifiedName("id"), "root"),
                    ElementBinding.Attribute(new QualifiedName("class"), "shell"),
                ],
                children:
                [
                    new ElementNode(
                        new QualifiedName("span"),
                        children: new[] { new TextNode("hello") }),
                ]);
        }
    }

    private sealed class ParentComponent : IComponent
    {
        private readonly ComponentReference _childReference;

        internal ParentComponent(ComponentReference childReference)
        {
            _childReference = childReference;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new ElementNode(
                new QualifiedName("main"),
                children:
                [
                    new ComponentNode(_childReference),
                    new ElementNode(
                        new QualifiedName("footer"),
                        children: new[] { new TextNode("tail") }),
                ]);
        }
    }

    private sealed class ChildComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return static _ => new ElementNode(
                new QualifiedName("section"),
                children: new[] { new TextNode("child") });
        }
    }

    private sealed class EmittingRootComponent : IComponent
    {
        private readonly ComponentReference _childReference;

        internal EmittingRootComponent(ComponentReference childReference)
        {
            _childReference = childReference;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Emit("root-ready", "root");
            return _ => new ElementNode(
                new QualifiedName("main"),
                children: new[] { new ComponentNode(_childReference) });
        }
    }

    private sealed class EmittingChildComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Emit("child-ready", "child");
            return static _ => new ElementNode(new QualifiedName("aside"));
        }
    }

    private sealed class ObservedComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Emit("ready");
            return static _ => new ElementNode(new QualifiedName("output"));
        }
    }

    private sealed class InteractiveComponent : IComponent
    {
        private readonly Reference<int> _count = new(0);

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new ElementNode(
                new QualifiedName("button"),
                bindings:
                [
                    ElementBinding.Event("click", (Action)(() => _count.Value++)),
                ],
                children: new[] { new TextNode(_count.Value.ToString()) });
        }
    }

    private sealed class ParameterComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            object? value = context.Bindings.Parameters["message"];
            return _ => new ElementNode(
                new QualifiedName("p"),
                children: new[] { new TextNode(value?.ToString() ?? string.Empty) });
        }
    }
}
