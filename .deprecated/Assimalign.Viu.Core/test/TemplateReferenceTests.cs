using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Tests;

// Pins template refs against @vue/runtime-core's rendererTemplateRef.ts setRef and the template-refs
// guide (https://vuejs.org/guide/essentials/template-refs.html): a ref-object receives the mounted
// element or a component's exposed surface, a function ref is invoked with the element/instance and
// nulled on unmount, application is post-flush (before user mounted hooks observe it), and a binding
// change unsets the old ref before setting the new. String refs are intentionally excluded
// ([V01.01.03.14]). Direct Render drains the post-flush queue synchronously, so a ref is observable
// immediately after Render returns.
public class TemplateReferenceTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public TemplateReferenceTests()
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
    public void ElementRefObject_IsSetToThePlatformNode_AfterMount()
    {
        var elementRef = new Reference<object?>(null);

        _renderer.Render(
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("ref", elementRef)), "content"),
            _container);

        elementRef.Value.ShouldNotBeNull();
        elementRef.Value.ShouldBeSameAs(_container.Children[0]);
    }

    [Fact]
    public void ElementRefObject_IsClearedToNull_OnUnmount()
    {
        var elementRef = new Reference<object?>(null);
        _renderer.Render(
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("ref", elementRef)), "x"),
            _container);
        elementRef.Value.ShouldNotBeNull();

        _renderer.Render(null, _container);

        elementRef.Value.ShouldBeNull();
    }

    [Fact]
    public void ComponentRefObject_ReceivesTheExposedSurface_NotTheRawInstance()
    {
        var exposed = new object();
        var component = new TestComponent
        {
            SetupFunction = (_, context) =>
            {
                context.Expose(exposed);
                return static () => VirtualNodeFactory.Element("input");
            },
        };
        var componentRef = new Reference<object?>(null);

        _renderer.Render(
            VirtualNodeFactory.Component(component, VirtualNodeFactory.Properties(("ref", componentRef))),
            _container);

        // Upstream getComponentPublicInstance: an exposed component surfaces only what it exposed.
        componentRef.Value.ShouldBeSameAs(exposed);
    }

    [Fact]
    public void ComponentRefObject_FallsBackToTheInstance_WhenNothingIsExposed()
    {
        ComponentInstance? instance = null;
        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                instance = ComponentInstance.Current;
                return static () => VirtualNodeFactory.Element("input");
            },
        };
        var componentRef = new Reference<object?>(null);

        _renderer.Render(
            VirtualNodeFactory.Component(component, VirtualNodeFactory.Properties(("ref", componentRef))),
            _container);

        // No Expose: the fallback surface is the component instance (Viu's stand-in for the
        // upstream public instance proxy).
        componentRef.Value.ShouldBeSameAs(instance);
    }

    [Fact]
    public void ComponentRefObject_IsClearedToNull_OnUnmount()
    {
        var exposed = new object();
        var component = new TestComponent
        {
            SetupFunction = (_, context) =>
            {
                context.Expose(exposed);
                return static () => VirtualNodeFactory.Element("input");
            },
        };
        var componentRef = new Reference<object?>(null);
        _renderer.Render(
            VirtualNodeFactory.Component(component, VirtualNodeFactory.Properties(("ref", componentRef))),
            _container);
        componentRef.Value.ShouldBeSameAs(exposed);

        _renderer.Render(null, _container);

        componentRef.Value.ShouldBeNull();
    }

    [Fact]
    public void FunctionRef_IsInvokedWithTheElementOnMount_AndNullOnUnmount()
    {
        var calls = new List<object?>();
        Action<object?> functionRef = value => calls.Add(value);

        _renderer.Render(
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("ref", functionRef)), "x"),
            _container);

        calls.Count.ShouldBe(1);
        calls[0].ShouldBeSameAs(_container.Children[0]);

        _renderer.Render(null, _container);

        calls.Count.ShouldBe(2);
        calls[1].ShouldBeNull();
    }

    [Fact]
    public void FunctionRef_IsInvokedPerElement_TheVForCollectionPattern()
    {
        var collected = new List<object?>();
        Action<object?> collector = value =>
        {
            if (value is not null)
            {
                collected.Add(value);
            }
        };

        _renderer.Render(
            VirtualNodeFactory.Element(
                "ul",
                (VirtualNodeProperties?)null,
                VirtualNodeFactory.Element("li", VirtualNodeFactory.Properties(("ref", collector)), "a"),
                VirtualNodeFactory.Element("li", VirtualNodeFactory.Properties(("ref", collector)), "b")),
            _container);

        // The same function ref is invoked once per element, enabling v-for-style collection.
        collected.Count.ShouldBe(2);
    }

    [Fact]
    public void PatchChangingTheBinding_UnsetsTheOldRefThenSetsTheNewInThatOrder()
    {
        var firstRef = new Reference<object?>(null);
        var secondRef = new Reference<object?>(null);

        _renderer.Render(
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("ref", firstRef)), "x"),
            _container);
        firstRef.Value.ShouldNotBeNull();

        // Same element, a different ref binding: the old ref is unset and the new one set.
        _renderer.Render(
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("ref", secondRef)), "x"),
            _container);

        firstRef.Value.ShouldBeNull();
        secondRef.Value.ShouldBeSameAs(_container.Children[0]);
    }

    [Fact]
    public void ElementRef_IsPopulatedBeforeTheMountedHookObservesIt()
    {
        var elementRef = new Reference<object?>(null);
        object? observedInMounted = null;
        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnMounted(() => observedInMounted = elementRef.Value);
                return () => VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("ref", elementRef)), "x");
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);

        // The ref (post-flush id -1) is applied before the user's OnMounted (post-flush, later id),
        // so the hook observes a populated ref (upstream: refs set before mounted).
        observedInMounted.ShouldNotBeNull();
        observedInMounted.ShouldBeSameAs(elementRef.Value);
    }

    [Fact]
    public void RefUpdate_IsScheduled_NotAppliedSynchronouslyOnMutation()
    {
        var swap = Reactive.Reference(false);
        var firstRef = new Reference<object?>(null);
        var secondRef = new Reference<object?>(null);

        using var effect = _renderer.Renderer.CreateRenderEffect(
            () => swap.Value
                ? VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("ref", secondRef)), "x")
                : VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("ref", firstRef)), "x"),
            _container);

        firstRef.Value.ShouldNotBeNull();
        secondRef.Value.ShouldBeNull();

        // Mutating the binding does not apply the ref synchronously — it is scheduled for the flush.
        swap.Value = true;
        firstRef.Value.ShouldNotBeNull();
        secondRef.Value.ShouldBeNull();

        _pump.RunUntilIdle();

        // After the flush (patch + post-flush): the old ref is cleared and the new one set.
        firstRef.Value.ShouldBeNull();
        secondRef.Value.ShouldBeSameAs(_container.Children[0]);
    }

    [Fact]
    public void StringRef_IsRejectedWithAWarning_AndCarriesNoBinding()
    {
        // String refs need a component instance proxy and are intentionally not ported: an invalid
        // ref value is reported (upstream dev warning) and treated as no ref.
        using var warnings = new WarningCapture();

        var node = VirtualNodeFactory.Element(
            "div", VirtualNodeFactory.Properties(("ref", "stringRef")), "x");

        node.Reference.ShouldBeNull();
        warnings.Messages.ShouldContain(message => message.Contains("Invalid template ref"));
    }

    [Fact]
    public void FunctionRefThrowingOnUnmount_RoutesThroughTheOwnersErrorChain()
    {
        // Upstream setRef routes a throwing function ref through callWithErrorHandling with the owning
        // component as context. Viu now threads parentComponent through unmount, so an unmount-time
        // throw reaches the owner's OnErrorCaptured chain instead of surfacing to the host — previously
        // the unmount path passed owner: null and the throw escaped ([V01.01.03.15.01]).
        Exception? captured = null;
        string? capturedInfo = null;
        Action<object?> throwingRef = value =>
        {
            if (value is null)
            {
                throw new InvalidOperationException("ref teardown boom");
            }
        };
        var child = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Element(
                "div", VirtualNodeFactory.Properties(("ref", throwingRef)), "x"),
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnErrorCaptured((exception, _, info) =>
                {
                    captured = exception;
                    capturedInfo = info;
                    return false; // handled
                });
                return () => VirtualNodeFactory.Element("section", VirtualNodeFactory.Component(child));
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container); // mount: ref(element), no throw

        // Unmount invokes the function ref with null (synchronous doSet); it throws and must be captured.
        Should.NotThrow(() => _renderer.Render(null, _container));

        captured.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("ref teardown boom");
        capturedInfo.ShouldNotBeNull();
        capturedInfo.ShouldContain("template ref");
    }
}
