using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Router;
using Assimalign.Viu.ServerRenderer;

using ViuRouter = Assimalign.Viu.Router.Router;
using ViuServerRenderer = Assimalign.Viu.ServerRenderer.ServerRenderer;

namespace Assimalign.Viu.Generators.Syntax.CompiledFixtureTests;

/// <summary>
/// Proves the unchanged single-file-component authoring surface against the adopted frame-based
/// generated-code and runtime application binary interface for <c>[V01.01.15.02]</c>. The suite
/// pins <c>[SFC-CG-1]</c> through <c>[SFC-CG-6]</c>, <c>[SFC-OPT-1]</c>,
/// <c>[RND-BLOCK-1]</c> through <c>[RND-BLOCK-5]</c>, and <c>[RND-FLAGS-5]</c>.
/// </summary>
public sealed class GeneratedComponentFixtureTests
{
    [Fact]
    public void Transition_ReferenceReadOnlyInsideAuthoredSlot_TracksBothToggleDirections()
    {
        // [BLT-7], [CMP-37]: the button does not read Visible; only the Transition slot reads it.
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        factory.Register(new ComponentRegistration(ComponentReference.ForName("Transition"),
            Transition.Registration.Contract, Transition.Registration.Activator));
        ComponentNode root = new(ComponentReference.ForName("ReactiveTransitionProbe"));
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, CreateApplication(root, factory));
        host.RunScheduledFlushes();
        CompiledFixtureNode button = host.FindElements("button").Single();
        int transitionUpdates = 0;
        renderer.GetMountedComponentViews(host.Container).Single(view => view.Instance is Transition)
            .Context.Lifecycle.OnUpdated(() => transitionUpdates++);
        for (int click = 0; click < 4; click++)
        {
            host.FindElements("p").Count.ShouldBe(click % 2 == 0 ? 1 : 0);
            InvokeHostListener(button.Bindings.Values.OfType<Delegate>().Single());
            host.RunScheduledFlushes();
            transitionUpdates.ShouldBe(click + 1);
            host.FindElements("p").Count.ShouldBe(click % 2 == 0 ? 0 : 1);
            host.FindElements("button").Single().ShouldBeSameAs(button);
        }
        renderer.Render(null, host.Container);
        host.Container.Children.ShouldBeEmpty();
    }

    [Fact]
    public async Task StyleBinding_ObjectDictionary_UsesHostPropertyAndUpdatesWithoutStringifying()
    {
        // [SFC-CG-2], [V01.01.06.07.01]: style objects must reach the host style patcher.
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentNode root = new(ComponentReference.ForName("StyleBindingProbe"));
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();
        ComponentFactory factory = CreateFactory(fixtures);
        renderer.Render(root, host.Container, CreateApplication(root, factory));
        host.RunScheduledFlushes();
        CompiledFixtureNode paragraph = host.FindElements("p").Single();
        paragraph.BindingKinds["style"].ShouldBe(ElementBindingKind.Property);
        paragraph.BindingKinds["class"].ShouldBe(ElementBindingKind.Property);
        host.FindElements("i").Single().BindingKinds["style"].ShouldBe(ElementBindingKind.Attribute);
        ((IReadOnlyDictionary<string, object?>)paragraph.Bindings["style"]!)["color"].ShouldBe("green");

        InvokeHostListener(host.FindElements("button").Single().Bindings.Values.OfType<Delegate>().Single());
        host.RunScheduledFlushes();
        ((IReadOnlyDictionary<string, object?>)paragraph.Bindings["style"]!)["color"].ShouldBe("gray");
        paragraph.Bindings["class"].ShouldBe("inactive");

        string markup = await ServerRender.RenderToStringAsync(new ServerRenderApplication(root, factory));
        markup.ShouldContain("color:green");
        markup.ShouldNotContain("Dictionary");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public async Task ScopedStyles_ProjectedSlotRoots_KeepAuthorScopeAndGainReceiverSuffix()
    {
        // [STY-1] Scoped CSS follows native authoring ownership, including projected slot roots.
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentNode root = new(ComponentReference.ForName("ScopedOwner"));
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();
        var factory = CreateFactory(fixtures);
        renderer.Render(root, host.Container, CreateApplication(root, factory));
        host.RunScheduledFlushes();
        var paragraph = host.FindElements("p").Single();
        string receiverScope = paragraph.Bindings.Keys.Single(name => name.StartsWith("data-v-", StringComparison.Ordinal));
        foreach (var span in host.FindElements("span"))
        {
            span.Bindings.Keys.ShouldContain(receiverScope + "-s");
            span.Bindings.Keys.ShouldNotContain(receiverScope);
            span.Bindings.Keys.Count(name => name.StartsWith("data-v-", StringComparison.Ordinal)).ShouldBe(2);
        }

        string markup = await ServerRender.RenderToStringAsync(new ServerRenderApplication(root, factory));
        markup.ShouldContain(receiverScope + "-s");
        markup.ShouldContain(">1</span>");
        renderer.Render(null, host.Container);
        host.Container.Children.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("SlotConditionalProbe")]
    [InlineData("RootConditionalProbe")]
    public void ConditionalSibling_ComponentModelToggles_DoNotWarnOrRemountFixedSibling(string componentName)
    {
        // PR #379 follow-up, [CMP-37], [RND-KEY-1]: identical fixed sibling positions
        // in a lazy slot and a component root retain compiler-generated branch identity.
        List<string> warnings = [];
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentNode root = new(ComponentReference.ForName(componentName));
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container,
            CreateApplication(root, CreateFactory(fixtures), warningHandler: warnings.Add));
        host.RunScheduledFlushes();
        CompiledFixtureNode button = host.FindElements("button").Single();
        CompiledFixtureNode fixedSibling = button.Parent!;
        object toggle = FindInstance(renderer, host, "ConditionalToggle");
        int toggleUpdates = 0;
        renderer.GetMountedComponentViews(host.Container).Single(view => ReferenceEquals(view.Instance, toggle))
            .Context.Lifecycle.OnUpdated(() => toggleUpdates++);

        for (int click = 0; click < 6; click++)
        {
            host.FindElements("p").Count.ShouldBe(click % 2 == 0 ? 1 : 0);
            InvokeHostListener(button.Bindings.Values.OfType<Delegate>().Single());
            host.RunScheduledFlushes();
            host.FindElements("p").Count.ShouldBe(click % 2 == 0 ? 0 : 1);
            button.Bindings["aria-checked"].ShouldBe(click % 2 != 0);
            host.FindElements("button").Single().ShouldBeSameAs(button);
            button.Parent.ShouldBeSameAs(fixedSibling);
            FindInstance(renderer, host, "ConditionalToggle").ShouldBeSameAs(toggle);
            toggleUpdates.ShouldBe(click + 1);
            warnings.ShouldBeEmpty();
        }

        host.MoveCount.ShouldBe(0);
        renderer.Render(null, host.Container);
        host.Container.Children.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Slot_ThrowingContentResetsDeclaringFrameAndCanRenderAgain()
    {
        // [CMP-37]: the outlet and declaring component use different frames. A failed
        // lazy slot must leave no incomplete block in the declaring mount's frame.
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentNode root = new(ComponentReference.ForName("ThrowingSlotProbe"));
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, CreateApplication(root, CreateFactory(fixtures)));
        host.RunScheduledFlushes();
        object instance = FindInstance(renderer, host, "ThrowingSlotProbe");
        ComponentRenderFrame frame = new(64);
        ComponentNode invocation = (ComponentNode)instance.GetType()
            .GetMethod("__ViuRender", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [instance, frame])!;
        ComponentSlot slot = invocation.Invocation.Slots["default"];
        Dictionary<string, object?> arguments = [];
        instance.GetType().GetField("MustThrow")!.SetValue(instance, true);

        Should.Throw<InvalidOperationException>(() => slot(arguments)).Message.ShouldBe("slot failure");
        Should.Throw<InvalidOperationException>(() => frame.CloseBlock());
        instance.GetType().GetField("MustThrow")!.SetValue(instance, false);
        slot(arguments).ShouldBeOfType<ElementNode>().RenderPlan.DynamicChildren.ShouldNotBeNull();
        Should.Throw<InvalidOperationException>(() => frame.CloseBlock());
        renderer.Render(null, host.Container);
    }

    [Theory]
    [InlineData("TasksPage")]
    [InlineData("TasksPageSibling")]
    public void TasksPage_SlottedFormAndConditionalKeyedList_UpdateModelDependents(string componentName)
    {
        // [CMP-37], [RND-BLOCK-2]: mirror the ci.4 two-root TasksPage and its Card outlets.
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        factory.Register(RouterLink.Registration);
        factory.Register(new ComponentRegistration(ComponentReference.ForName("RouterLink"),
            RouterLink.Registration.Contract, RouterLink.Registration.Activator));
        ComponentNode root = new(ComponentReference.ForName(componentName));
        using ViuRouter router = new(RouterHistory.CreateMemory(), []);
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, CreateApplication(root, factory, new RouterServiceProvider(router)));
        host.RunScheduledFlushes();
        object instance = FindInstance(renderer, host, componentName);
        MountedComponentView<CompiledFixtureNode>[] cards = renderer.GetMountedComponentViews(host.Container)
            .Where(view => view.Instance.GetType().Name == "Card").ToArray();
        int formUpdates = 0;
        int listUpdates = 0;
        int pageUpdates = 0;
        cards[0].Context.Lifecycle.OnUpdated(() => formUpdates++);
        cards[1].Context.Lifecycle.OnUpdated(() => listUpdates++);
        renderer.GetMountedComponentViews(host.Container).Single(view => ReferenceEquals(view.Instance, instance))
            .Context.Lifecycle.OnUpdated(() => pageUpdates++);
        CompiledFixtureNode input = host.FindElements("input").Single(node =>
            node.Bindings.TryGetValue("aria-label", out object? value) && Equals(value, "Task title"));
        CompiledFixtureNode button = host.FindElements("button").Single(node =>
            node.Bindings.TryGetValue("type", out object? value) && Equals(value, "submit"));
        button.DescendantText.ShouldBe("Add []");
        button.Bindings["disabled"].ShouldBe(true);

        input.ModelBinding.ShouldNotBeNull().Setter("From JS");
        instance.GetType().GetProperty("NewTitleText")!.GetValue(instance).ShouldBe("From JS");
        host.RunScheduledFlushes();
        button.DescendantText.ShouldBe("Add [From JS]");
        button.Bindings["disabled"].ShouldBe(false);
        formUpdates.ShouldBe(1);
        listUpdates.ShouldBe(0);
        pageUpdates.ShouldBe(0);
        if (componentName == "TasksPageSibling")
        {
            host.FindElements("small").Single().DescendantText.ShouldBe("[From JS]");
        }
        input.ModelBinding!.Setter("From JS");
        host.RunScheduledFlushes();
        formUpdates.ShouldBe(1);
        input.ModelBinding.Setter(string.Empty);
        host.RunScheduledFlushes();
        formUpdates.ShouldBe(2);
        button.DescendantText.ShouldBe("Add []");
        button.Bindings["disabled"].ShouldBe(true);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public async Task NativeHandlerShapes_ModifierAndKeyWrappersRetainBrowserDelegatesAndGuards()
    {
        // [SFC-CG-11] Sync/Task handlers, with/without an event, all retain the concrete Browser event.
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentNode root = new(ComponentReference.ForName("HandlerShapes"));
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, CreateApplication(root, CreateFactory(fixtures)));
        host.RunScheduledFlushes();
        object instance = FindInstance(renderer, host, "HandlerShapes");
        foreach (CompiledFixtureNode node in host.FindElements("button").Concat(host.FindElements("input")))
        {
            Delegate listener = node.Bindings.Values.OfType<Delegate>().Single();
            listener.GetType().ShouldBe(node.Bindings.ContainsKey("data-async")
                ? typeof(Func<BrowserEvent, Task>) : typeof(Action<BrowserEvent>));
            bool keyboard = node.Description == "input";
            BrowserEvent rejected = new(keyboard ? "keydown" : "click", 0, "Escape", "", BrowserEventModifiers.None,
                -1, 0, 0, 0, 0, false, null, false, null);
            BrowserEvent accepted = new(keyboard ? "keydown" : "click", 0, "Enter", "", BrowserEventModifiers.None,
                -1, 0, 0, 0, 0, true, null, false, null);
            int previous = GetIntProperty(instance, "Calls");
            await InvokeNativeHandlerAsync(listener, rejected);
            GetIntProperty(instance, "Calls").ShouldBe(previous);
            await InvokeNativeHandlerAsync(listener, accepted);
            GetIntProperty(instance, "Calls").ShouldBe(previous + 1);
            if (!keyboard)
            {
                accepted.DefaultPrevented.ShouldBeTrue();
                accepted.PropagationStopped.ShouldBeTrue();
            }
        }
        GetIntProperty(instance, "Calls").ShouldBe(16);
        renderer.Render(null, host.Container);
    }

    private static async Task InvokeNativeHandlerAsync(Delegate handler, BrowserEvent value)
    {
        switch (handler)
        {
            case Action<BrowserEvent> synchronous:
                handler.GetType().ShouldBe(typeof(Action<BrowserEvent>));
                synchronous(value);
                break;
            case Func<BrowserEvent, Task> asynchronous:
                handler.GetType().ShouldBe(typeof(Func<BrowserEvent, Task>));
                await asynchronous(value);
                break;
            default:
                throw new InvalidOperationException("Generated native handler lost its Browser event type.");
        }
    }

    [Fact]
    public void ScalarModels_AllNumericAndNullableTargetsConvertWithoutTypeErasure()
    {
        // [SFC-CG-12] All supported numeric target declarations compile and receive typed values.
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentNode root = new(ComponentReference.ForName("ScalarModels"));
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, CreateApplication(root, CreateFactory(fixtures)));
        host.RunScheduledFlushes();
        object instance = FindInstance(renderer, host, "ScalarModels");
        CompiledFixtureNode[] inputs = host.FindElements("input").ToArray();
        for (int index = 0; index < inputs.Length; index++)
        {
            ModelBinding binding = inputs[index].ModelBinding.ShouldNotBeNull();
            binding.Setter(13d);
            PropertyInfo property = instance.GetType().GetProperty("Value" + index)!;
            Convert.ToDecimal(property.GetValue(instance)).ShouldBe(13m);
            if (index % 2 != 0)
            {
                binding.Setter("");
                property.GetValue(instance).ShouldBeNull();
            }
        }
        renderer.Render(null, host.Container);
    }

    [Theory]
    [InlineData("InteractionProbe")]
    [InlineData("SingleSlotModelProbe")]
    public void SlotModel_BrowserDispatch_CommitsParentAndRerendersDependents(string componentName)
    {
        // [SFC-CG-7] Exercise shipping Browser directives, not just the generated setter.
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        ComponentNode root = new(ComponentReference.ForName(componentName), new ComponentInvocation(
            arguments: new Dictionary<string, object?> { ["title"] = "slotted" }));
        ApplicationContext application = new(new ApplicationOptions
        {
            RootComponent = root,
            Components = factory,
            Directives = new DirectiveRegistry([new KeyValuePair<Type, IDirective>(typeof(VModelText), VModelText.Instance)]),
            ErrorHandler = (error, _, _) => throw new InvalidOperationException("Browser dispatch failed.", error),
        });
        Queue<Action> scheduled = [];
        Scheduler.Reset();
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(scheduled.Enqueue);
        try
        {
            List<byte[]> frames = [];
            var host = new BrowserRendererHost((frame, length) =>
            {
                frames.Add(frame.AsSpan(0, length).ToArray());
                return [];
            });
            host.ObserveForeignHandle(1000);
#pragma warning disable CA1416 // Injected command applier and DOM-less dispatch avoid JavaScript imports.
            using IDisposable activation = host.Activate();
            Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
            renderer.Render(root, 1000, application);
            while (scheduled.TryDequeue(out Action? flush)) { flush(); }
            object parent = renderer.GetMountedComponentViews(1000).Single(view =>
                view.Instance.GetType().Name == componentName).Instance;
            int input = (int)parent.GetType().GetField("Input")!.GetValue(parent)!;
            frames.Clear();
            host.DispatchEvent(input, false, new BrowserEvent("input", 0, "", "", BrowserEventModifiers.None,
                -1, 0, 0, 0, 0, true, "from slot", false, null));
#pragma warning restore CA1416
            GetReferenceValue(parent, "Model").ShouldBe("from slot");
            while (scheduled.TryDequeue(out Action? flush)) { flush(); }
            Encoding.UTF8.GetString(frames.SelectMany(frame => frame).ToArray()).ShouldContain("from slot");
            renderer.Render(null, 1000, application);
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    [Fact]
    public void TypedBindings_ConvertDeclaredTargetsAndInvokeTypedComponentEvents()
    {
        // [SFC-CG-12] Every carrier setter compiles against its target's declared type.
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        ComponentNode root = new(ComponentReference.ForName("BindingSeams"));
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, CreateApplication(root, factory));
        host.RunScheduledFlushes();
        object instance = FindInstance(renderer, host, "BindingSeams");
        CompiledFixtureNode[] inputs = host.FindElements("input").ToArray();
        int getterReads = GetIntProperty(instance, "ReferenceGetterCount");
        object?[] values = ["typed", true, 12d, "", "trimmed", true, 4.5d, "counted", null, true];
        for (int index = 0; index < values.Length; index++)
        {
            inputs[index].ModelBinding.ShouldNotBeNull().Setter(values[index]);
        }
        instance.GetType().GetProperty("Text")!.GetValue(instance).ShouldBe("typed");
        instance.GetType().GetProperty("Checked")!.GetValue(instance).ShouldBe(true);
        instance.GetType().GetProperty("Count")!.GetValue(instance).ShouldBe(12);
        Should.Throw<FormatException>(() => inputs[2].ModelBinding!.Setter("invalid"));
        instance.GetType().GetProperty("NullableCount")!.GetValue(instance).ShouldBeNull();
        GetReferenceValue(instance, "TextReference").ShouldBe("trimmed");
        GetReferenceValue(instance, "CheckedReference").ShouldBe(true);
        GetReferenceValue(instance, "CountReference").ShouldBe(4.5m);
        GetIntProperty(instance, "ReferenceGetterCount").ShouldBe(getterReads + 1);
        instance.GetType().GetProperty("NullableText")!.GetValue(instance).ShouldBeNull();
        instance.GetType().GetProperty("NullableChecked")!.GetValue(instance).ShouldBe(true);
        instance.GetType().GetProperty("NullableText")!.SetValue(instance, "before component update");
        Delegate listener = host.FindElements("button").Single().Bindings.Values.OfType<Delegate>().Single();
        InvokeHostListener(listener);
        GetIntProperty(instance, "LastChanged").ShouldBe(7);
        GetIntProperty(instance, "LastFinished").ShouldBe(9);
        InvokeHostListener(host.FindElements("a").Single().Bindings.Values.OfType<Delegate>().Single());
        GetIntProperty(instance, "Count").ShouldBe(17);
        instance.GetType().GetProperty("Checked")!.GetValue(instance).ShouldBe(false);
        GetReferenceValue(instance, "TextReference").ShouldBe("component");
        instance.GetType().GetProperty("NullableText")!.GetValue(instance).ShouldBeNull();
        fixtures.GeneratedSources.Single(pair => pair.Key.EndsWith("BindingSeams.SingleFileComponent.g.cs", StringComparison.Ordinal))
            .Value.ShouldContain("global::ViuCompiledFixtures.BindingSeams.Format");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public async Task ServerTargetedFixture_CompiledAndTraversalProfiles_AreByteIdentical()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        var registry = new ServerRenderRegistry();
        ComponentFactory components = CreateFactory(fixtures);
        var root = new ComponentNode(ComponentReference.ForName("TargetedTextProbe"));
        var application = new ServerRenderApplication(root, components);

        fixtures.RegisterServerRenders(registry);

        registry.TryResolve(
                ComponentReference.ForName("TargetedTextProbe"),
                out ServerRenderRegistration? registration)
            .ShouldBeTrue();
        registration.ShouldNotBeNull();

        string traversal = await ViuServerRenderer.RenderToStringAsync(application);
        string compiled = await ViuServerRenderer.RenderToStringAsync(application, registry);

        compiled.ShouldBe(traversal);
    }

    [Fact]
    public async Task MathMlStaticProbe_CompiledAndTraversalProfiles_AreByteIdentical()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        var registry = new ServerRenderRegistry();
        ComponentFactory components = CreateFactory(fixtures);
        var root = new ComponentNode(ComponentReference.ForName("MathStaticProbe"));
        var application = new ServerRenderApplication(root, components);
        const string expected =
            "<math><mi mathcolor=\"red\">a</mi><mi mathcolor=\"red\">b</mi>" +
            "<mi mathcolor=\"red\">c</mi><mi mathcolor=\"red\">d</mi>" +
            "<mi mathcolor=\"red\">e</mi><mi mathcolor=\"blue\">dynamic math</mi></math>";

        fixtures.RegisterServerRenders(registry);

        string traversal = await ViuServerRenderer.RenderToStringAsync(application);
        string compiled = await ViuServerRenderer.RenderToStringAsync(application, registry);

        traversal.ShouldBe(expected);
        compiled.ShouldBe(traversal);
    }

    [Fact]
    public void CompiledRoot_AndCodeFirstDefinition_ComposeAndUpdateInOneApplication()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        IReactiveReference<string>? message = null;
        ComponentRegistration codeFirst = ComponentRegistration.Define(
            "CodeFirstChild",
            new ComponentContract(renderCacheSize: 0, displayName: "CodeFirstChild"),
            _ =>
            {
                message = Reactive.Reference("code-first");
                return _ => new ElementNode(
                    new QualifiedName("strong"),
                    children: [new TextNode(message.Value)]);
            });
        factory.Register(codeFirst);
        ComponentNode root = new(ComponentReference.ForName("MixedAuthoring"));
        ApplicationContext application = CreateApplication(root, factory);
        using CompiledFixtureHost host = new();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldBe("code-first");

        message.ShouldNotBeNull();
        message.Value = "composed";
        host.RunScheduledFlushes();

        host.Container.DescendantText.ShouldBe("composed");
        host.TextChangeCount.ShouldBe(1);
        renderer.GetMountedComponentViews(host.Container)
            .Select(view => view.Request.Component.RegisteredName)
            .ShouldContain("MixedAuthoring");
        renderer.GetMountedComponentViews(host.Container)
            .Select(view => view.Request.Component.RegisteredName)
            .ShouldContain("CodeFirstChild");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void GeneratedComponent_MountsThroughBrowserCommandHost_InOneCommandFrame()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        ComponentNode root = new(ComponentReference.ForName("TargetedTextProbe"));
        ApplicationContext application = CreateApplication(root, factory);
        List<int> frameLengths = [];
        var host = new BrowserRendererHost(
            (_, length) =>
            {
                frameLengths.Add(length);
                return [];
            });
        const int container = 1000;
        host.ObserveForeignHandle(container);
        Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, container, application);

        frameLengths.ShouldHaveSingleItem().ShouldBeGreaterThan(0);
        host.InteropCallCount.ShouldBe(1);
        renderer.GetMountedComponentViews(container)
            .ShouldHaveSingleItem()
            .Instance.GetType().Name.ShouldBe("TargetedTextProbe");

        renderer.Render(null, container, application);
        host.InteropCallCount.ShouldBe(2);
    }

    [Fact]
    public void NullableReferenceParameter_GeneratedContractCompilesAndMountsThroughShippingRuntime()
    {
        // [CMP-26]/[SFC-CG-3] The generated contract carries the runtime string type, while
        // [CMP-29] keeps the nullable annotation on the authored property and its binding path.
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        string generated = fixtures.GeneratedSources
            .Single(pair => pair.Key.EndsWith(
                "NullableParameterProbe.SingleFileComponent.g.cs",
                StringComparison.Ordinal))
            .Value;
        ComponentFactory factory = CreateFactory(fixtures);
        var invocation = new ComponentInvocation(
            arguments: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = "optional",
            });
        ComponentNode root = new(
            ComponentReference.ForName("NullableParameterProbe"),
            invocation);
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, CreateApplication(root, factory));
        host.RunScheduledFlushes();

        generated.ShouldContain(
            "ComponentParameter(\"label\", parameterType: typeof(string))");
        generated.ShouldContain(
            "ComponentParameter(\"values\", parameterType: " +
            "global::Assimalign.Viu.Generated.RenderGlue.ParameterRuntimeType<List<string>?>())");
        generated.ShouldNotContain("typeof(string?)");
        generated.ShouldNotContain("typeof(List<string>?)");
        host.Container.DescendantText.ShouldBe("optional");
        renderer.GetMountedComponentViews(host.Container)
            .ShouldHaveSingleItem()
            .Instance.GetType().Name.ShouldBe("NullableParameterProbe");

        renderer.Render(null, host.Container);
    }

    [Fact]
    public void ShippingFixtures_MountBindFallThroughAndEmitThroughTheAdoptedModel()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        var cardInvocation = new ComponentInvocation(
            arguments: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Compiled templates",
                ["data-extra"] = "fallthrough",
            });
        ComponentNode cardRoot = new(
            ComponentReference.ForName("AttributedCard"),
            cardInvocation);
        using (var cardHost = new CompiledFixtureHost())
        {
            Renderer<CompiledFixtureNode> renderer = cardHost.CreateRenderer();
            renderer.Render(cardRoot, cardHost.Container, CreateApplication(cardRoot, factory));
            cardHost.RunScheduledFlushes();

            cardHost.Container.DescendantText.ShouldBe("FeatureCompiled templates");
            CompiledFixtureNode article = cardHost.FindElements("article").ShouldHaveSingleItem();
            article.Bindings["data-extra"].ShouldBe("fallthrough");
            renderer.Render(null, cardHost.Container);
        }

        var emitted = new Dictionary<string, List<IReadOnlyList<object?>>>(StringComparer.Ordinal)
        {
            ["changed"] = [],
            ["dismissed"] = [],
        };
        var ratingInvocation = new ComponentInvocation(
            listeners: new Dictionary<string, ComponentEventListener>(StringComparer.Ordinal)
            {
                ["changed"] = arguments => emitted["changed"].Add(arguments),
                ["dismissed"] = arguments => emitted["dismissed"].Add(arguments),
            });
        ComponentNode ratingRoot = new(
            ComponentReference.ForName("Rating"),
            ratingInvocation);
        using var ratingHost = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> ratingRenderer = ratingHost.CreateRenderer();
        ratingRenderer.Render(
            ratingRoot,
            ratingHost.Container,
            CreateApplication(ratingRoot, factory));
        ratingHost.RunScheduledFlushes();
        object rating = FindInstance(ratingRenderer, ratingHost, "Rating");

        rating.GetType().GetMethod("Changed")!.Invoke(rating, [4]);
        rating.GetType().GetMethod("Dismissed")!.Invoke(rating, null);

        emitted["changed"].ShouldHaveSingleItem().ShouldBe([4]);
        emitted["dismissed"].ShouldHaveSingleItem().ShouldBeEmpty();
        ratingRenderer.Render(null, ratingHost.Container);
    }

    [Fact]
    public void GeneratedSources_CompileAgainstShippingRuntime_AndContainNoSupersededAbi()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        string generated = string.Join("\n", fixtures.GeneratedSources.Values);

        generated.ShouldNotContain("using static");
        generated.ShouldNotContain("Render" + "Helpers");
        generated.ShouldNotContain("Block" + "Token");
        generated.ShouldNotContain("IComponent" + "Template");
        generated.ShouldNotContain("IComponent" + "Context");
        generated.ShouldNotContain("IComponentHotReload" + "Metadata");
        generated.ShouldNotContain("Scope" + "Identifier");
        Regex.Matches(generated, @"(?<![A-Za-z0-9_])_[A-Za-z][A-Za-z0-9_]*")
            .Select(match => match.Value)
            .ShouldBeEmpty();
        generated.ShouldContain("ComponentRenderFrame");
        generated.ShouldContain("new global::Assimalign.Viu.Components.RenderPlan(");
        generated.ShouldContain("class GeneratedViuComponents");
    }

    [Fact]
    public void TargetedTextProbe_MountsAndTargetsOnlyTheCompiledDynamicTextBlock()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        ComponentNode root = new(ComponentReference.ForName("TargetedTextProbe"));
        ApplicationContext application = CreateApplication(root, factory);
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldBe("staticfirst");
        object instance = FindInstance(renderer, host, "TargetedTextProbe");
        host.ResetOperationCounts();
        Renderer<CompiledFixtureNode>.PatchVisitCount = 0;

        SetReferenceValue(instance, "Message", "second");
        host.RunScheduledFlushes();

        host.Container.DescendantText.ShouldBe("staticsecond");
        host.TextChangeCount.ShouldBe(1);
        Renderer<CompiledFixtureNode>.PatchVisitCount.ShouldBe(3);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public async Task RouterOutlet_NavigationSwapsGeneratedViewsWithRepeatedCachedStaticChildren()
    {
        // [RND-2]/[RND-4]/[SFC-OPT-1] Each generated row passes one cached static slot
        // description to two tracked slot outlets. The transition-wrapped route swap must retain
        // two distinct mounted occurrences per row through the mounted-triggered update.
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        RegisterRouterOutletDependencies(factory);
        ComponentNode firstRoute = new(ComponentReference.ForName("RouterFirstView"));
        ComponentNode repeatedRoute = new(
            ComponentReference.ForName("RouterRepeatedStaticView"));
        using var router = new ViuRouter(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/first", component: firstRoute),
                new RouteRecord("/repeated", component: repeatedRoute),
            ]);
        (await router.PushAsync("/first")).ShouldBeNull();
        ComponentNode root = new(ComponentReference.ForName("RouterOutletShell"));
        ApplicationContext application = CreateApplication(
            root,
            factory,
            new RouterServiceProvider(router));
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        host.RunScheduledFlushes();
        MountedComponentView<CompiledFixtureNode> firstMounted = renderer
            .GetMountedComponentViews(host.Container)
            .Single(view => string.Equals(
                view.Instance.GetType().Name,
                "RouterFirstView",
                StringComparison.Ordinal));
        host.Container.DescendantText.ShouldContain("first compiled route");

        (await router.PushAsync("/repeated")).ShouldBeNull();
        host.RunScheduledFlushes();

        firstMounted.IsMounted.ShouldBeFalse();
        string[] mountedNames = renderer.GetMountedComponentViews(host.Container)
            .Select(view => view.Instance.GetType().Name)
            .ToArray();
        mountedNames.ShouldNotContain("RouterFirstView");
        mountedNames.ShouldContain("RouterRepeatedStaticView");
        MountedComponentView<CompiledFixtureNode> repeatedMounted = renderer
            .GetMountedComponentViews(host.Container)
            .Single(view => string.Equals(
                view.Instance.GetType().Name,
                "RouterRepeatedStaticView",
                StringComparison.Ordinal));
        MountedComponentView<CompiledFixtureNode>[] aliasedHosts = renderer
            .GetMountedComponentViews(host.Container)
            .Where(view => string.Equals(
                view.Instance.GetType().Name,
                "RouterAliasedSlotHost",
                StringComparison.Ordinal))
            .ToArray();
        aliasedHosts.Length.ShouldBe(3);
        host.FindElements("li").Count.ShouldBe(3);
        IReadOnlyList<CompiledFixtureNode> repeatedSpans = host.FindElements("span");
        repeatedSpans.Count.ShouldBe(3);
        repeatedSpans.ShouldAllBe(
            span => Equals(span.Bindings["class"], "signal-dot"));
        IReadOnlyList<CompiledFixtureNode> replacements = host.FindElements("strong");
        replacements.Count.ShouldBe(3);
        replacements.ShouldAllBe(
            strong => Equals(strong.Bindings["class"], "replacement"));
        host.Container.DescendantText.ShouldContain("reference:tracked");
        host.Container.DescendantText.ShouldContain("computed:cached");
        host.Container.DescendantText.ShouldContain("effect:scheduled");
        string repeatedGenerated = fixtures.GeneratedSources
            .Single(pair => pair.Key.EndsWith(
                "RouterRepeatedStaticView.SingleFileComponent.g.cs",
                StringComparison.Ordinal))
            .Value;
        repeatedGenerated.ShouldContain(
            "frame.GetOrAddCache<global::Assimalign.Viu.Components.VirtualNode?>");
        repeatedGenerated.ShouldContain("\"signal-dot\"");
        string hostGenerated = fixtures.GeneratedSources
            .Single(pair => pair.Key.EndsWith(
                "RouterAliasedSlotHost.SingleFileComponent.g.cs",
                StringComparison.Ordinal))
            .Value;
        // [SSR-TARGET-2] The fixture is dual-targeted: each deterministic profile tracks the same
        // two authored slot nodes, so the combined generated partial contains four calls.
        Regex.Matches(hostGenerated, @"frame\.Track\(slotNode\d+\)").Count.ShouldBe(4);

        (await router.PushAsync("/first")).ShouldBeNull();
        host.RunScheduledFlushes();

        repeatedMounted.IsMounted.ShouldBeFalse();
        aliasedHosts.ShouldAllBe(view => !view.IsMounted);
        host.FindElements("li").ShouldBeEmpty();
        host.Container.DescendantText.ShouldContain("first compiled route");

        renderer.Render(null, host.Container);
    }

    [Fact]
    public async Task RouterOutlet_NavigationIntoGeneratedBuiltInsView_MountsTransitionGroupKeepAliveAndTeleportThroughBrowserHost()
    {
        // [RND-HOST-1]/[RND-BLOCK-5] This is the packaged-showcase shape: the compiled route
        // outlet's out-in transition synchronously mounts a generated view whose subtree owns
        // another transition, TransitionGroup, KeepAlive, and Teleport. The Browser host must
        // accept every renderer-owned node created during that re-entrant mount.
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        RegisterRouterOutletDependencies(factory);
        RegisterTransitionGroup(factory);
        ComponentNode firstRoute = new(ComponentReference.ForName("RouterFirstView"));
        ComponentNode builtInsRoute = new(ComponentReference.ForName("RouterBuiltInsView"));
        using var router = new ViuRouter(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/first", component: firstRoute),
                new RouteRecord("/built-ins", component: builtInsRoute),
            ]);
        (await router.PushAsync("/first")).ShouldBeNull();
        ComponentNode root = new(ComponentReference.ForName("RouterOutletShell"));
        List<Exception> errors = [];
        ApplicationContext application = CreateApplication(
            root,
            factory,
            new RouterServiceProvider(router),
            (error, _, _) => errors.Add(error));
        Queue<Action> scheduledFlushes = [];
        int teleportResolutionCount = 0;
        Scheduler.Reset();
        using IDisposable schedulerRegistration =
            Scheduler.UseFlushDispatcher(scheduledFlushes.Enqueue);
        var host = new BrowserRendererHost((_, _) => []);
        const int container = 1000;
        const int teleportTarget = 2000;
        host.ObserveForeignHandle(container);
        host.ObserveForeignHandle(teleportTarget);
        RendererOptions<int> browserOptions = host.Options;
        RendererOptions<int> options = CopyBrowserOptions(
            browserOptions,
            selector =>
            {
                selector.ShouldBe("#compiled-overlay");
                teleportResolutionCount++;
                return teleportTarget;
            });
        Renderer<int> renderer = RendererFactory.CreateRenderer(options);

        try
        {
            renderer.Render(root, container, application);
            RunScheduledFlushes(scheduledFlushes);
            MountedComponentView<int> firstMounted = renderer
                .GetMountedComponentViews(container)
                .Single(view => string.Equals(
                    view.Instance.GetType().Name,
                    "RouterFirstView",
                    StringComparison.Ordinal));

            (await router.PushAsync("/built-ins")).ShouldBeNull();
            RunScheduledFlushes(scheduledFlushes);

            firstMounted.IsMounted.ShouldBeFalse();
            string[] mountedNames = renderer.GetMountedComponentViews(container)
                .Select(view => view.Instance.GetType().Name)
                .ToArray();
            mountedNames.ShouldContain("RouterBuiltInsView");
            mountedNames.ShouldContain("TransitionGroup");
            mountedNames.ShouldContain("TargetedTextProbe");
            teleportResolutionCount.ShouldBe(1);
            errors.ShouldBeEmpty();
            string generated = fixtures.GeneratedSources
                .Single(pair => pair.Key.EndsWith(
                    "RouterBuiltInsView.SingleFileComponent.g.cs",
                    StringComparison.Ordinal))
                .Value;
            generated.ShouldContain("ComponentReference.ForName(\"Transition\")");
            generated.ShouldContain("ComponentReference.ForName(\"TransitionGroup\")");
            generated.ShouldContain("KeepAliveNode");
            generated.ShouldContain("TeleportNode");

            renderer.Render(null, container, application);
            RunScheduledFlushes(scheduledFlushes);
            errors.ShouldBeEmpty();
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    [Fact]
    public async Task RouterOutlet_SynchronousLeaveGeneratedMountFailure_EscapesErrorHandler()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        RegisterRouterOutletDependencies(factory);
        RegisterTransitionGroup(factory);
        ComponentNode firstRoute = new(ComponentReference.ForName("RouterFirstView"));
        ComponentNode builtInsRoute = new(ComponentReference.ForName("RouterBuiltInsView"));
        using var router = new ViuRouter(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/first", component: firstRoute),
                new RouteRecord("/built-ins", component: builtInsRoute),
            ]);
        (await router.PushAsync("/first")).ShouldBeNull();
        ComponentNode root = new(ComponentReference.ForName("RouterOutletShell"));
        List<Exception> errors = [];
        ApplicationContext application = CreateApplication(
            root,
            factory,
            new RouterServiceProvider(router),
            (error, _, _) => errors.Add(error));
        Queue<Action> scheduledFlushes = [];
        Scheduler.Reset();
        using IDisposable schedulerRegistration =
            Scheduler.UseFlushDispatcher(scheduledFlushes.Enqueue);
        var host = new BrowserRendererHost((_, _) => []);
        const int container = 3000;
        const int teleportTarget = 4000;
        host.ObserveForeignHandle(container);
        host.ObserveForeignHandle(teleportTarget);
        RendererOptions<int> browserOptions = host.Options;
        RendererOptions<int> options = CopyBrowserOptions(
            browserOptions,
            _ => teleportTarget,
            name => name.LocalName == "article"
                ? throw new InvalidOperationException("generated incoming mount failed")
                : browserOptions.CreateElement(name));
        Renderer<int> renderer = RendererFactory.CreateRenderer(options);
        Exception? cleanupFailure = null;

        try
        {
            renderer.Render(root, container, application);
            RunScheduledFlushes(scheduledFlushes);
            (await router.PushAsync("/built-ins")).ShouldBeNull();

            Action flush = () => RunScheduledFlushes(scheduledFlushes);

            InvalidOperationException exception =
                flush.ShouldThrow<InvalidOperationException>();
            exception.Message.ShouldBe("generated incoming mount failed");
            errors.ShouldBeEmpty();
        }
        finally
        {
            try
            {
                renderer.Render(null, container, application);
                RunScheduledFlushes(scheduledFlushes);
            }
            catch (Exception exception)
            {
                cleanupFailure = exception;
            }
            finally
            {
                Scheduler.Reset();
            }
        }

        cleanupFailure.ShouldBeNull();
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void PatchProbe_PreservesIfForKeyedMovesAndMemoSemantics()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        ComponentNode root = new(ComponentReference.ForName("PatchProbe"));
        ApplicationContext application = CreateApplication(root, factory);
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, application);
        host.RunScheduledFlushes();
        object instance = FindInstance(renderer, host, "PatchProbe");

        host.ResetOperationCounts();
        SetReferenceValue(instance, "Items", new[] { "beta", "alpha" });
        host.RunScheduledFlushes();
        host.MoveCount.ShouldBeGreaterThan(0);
        host.Container.DescendantText.IndexOf("betaalpha", StringComparison.Ordinal)
            .ShouldBeGreaterThanOrEqualTo(0);

        SetReferenceValue(instance, "Show", false);
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldNotContain("shown");

        host.ResetOperationCounts();
        SetReferenceValue(instance, "MemoMessage", "memo-blocked");
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldContain("memo-first");
        host.Container.DescendantText.ShouldNotContain("memo-blocked");
        host.TextChangeCount.ShouldBe(0);
        host.BindingPatchCount.ShouldBe(0);
        host.MoveCount.ShouldBe(0);

        host.ResetOperationCounts();
        SetReferenceValue(instance, "MemoKey", 2);
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldContain("memo-blocked");
        host.TextChangeCount.ShouldBe(1);
        host.BindingPatchCount.ShouldBe(0);
        host.MoveCount.ShouldBe(0);

        host.ResetOperationCounts();
        SetReferenceValue(
            instance,
            "MemoItems",
            new[] { new KeyValuePair<int, string>(1, "memo-row-other") });
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldContain("memo-row-first");
        host.Container.DescendantText.ShouldNotContain("memo-row-other");
        host.TextChangeCount.ShouldBe(0);
        host.BindingPatchCount.ShouldBe(0);
        host.MoveCount.ShouldBe(0);

        host.ResetOperationCounts();
        SetReferenceValue(
            instance,
            "MemoItems",
            new[] { new KeyValuePair<int, string>(1, "memo-row-updated") });
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldContain("memo-row-updated");
        host.TextChangeCount.ShouldBe(1);
        host.BindingPatchCount.ShouldBe(0);
        host.MoveCount.ShouldBe(0);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void PascalCaseNativeCollisions_CompileMountFlowParametersAndRaiseEvents()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        ComponentNode root = new(ComponentReference.ForName("NativeCollisionHost"));
        ApplicationContext application = CreateApplication(root, factory);
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        host.RunScheduledFlushes();

        string[] mountedNames = renderer.GetMountedComponentViews(host.Container)
            .Select(view => view.Instance.GetType().Name)
            .ToArray();
        mountedNames.ShouldContain("NativeCollisionHost");
        mountedNames.ShouldContain("Button");
        mountedNames.ShouldContain("Input");
        host.Container.DescendantText.ShouldContain("component button");
        host.Container.DescendantText.ShouldContain("component input");
        host.Container.DescendantText.ShouldContain("native button");
        host.FindElements("input").ShouldHaveSingleItem()
            .Bindings["aria-label"].ShouldBe("native input");

        CompiledFixtureNode componentButton = host.FindElements("button")
            .Single(button => button.DescendantText == "component button");
        InvokeHostListener(componentButton.Bindings.Values.OfType<Delegate>().ShouldHaveSingleItem());
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldContain("button:component button");

        CompiledFixtureNode componentInput = host.FindElements("button")
            .Single(button => button.DescendantText == "component input");
        InvokeHostListener(componentInput.Bindings.Values.OfType<Delegate>().ShouldHaveSingleItem());
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldContain("input:component input");

        string generated = fixtures.GeneratedSources
            .Single(pair => pair.Key.EndsWith(
                "NativeCollisionHost.SingleFileComponent.g.cs",
                StringComparison.Ordinal))
            .Value;
        generated.ShouldContain("ComponentReference.ForName(\"Button\")");
        generated.ShouldContain("ComponentReference.ForName(\"Input\")");
        generated.ShouldContain("QualifiedName(\"button\")");
        generated.ShouldContain("QualifiedName(\"input\")");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void MathMlStaticRun_InsertsOnceAndDynamicSiblingUpdatesWithoutReinsertion()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        ComponentNode root = new(ComponentReference.ForName("MathStaticProbe"));
        ApplicationContext application = CreateApplication(root, factory);
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        host.RunScheduledFlushes();

        host.StaticInsertionCount.ShouldBe(1);
        host.LastStaticFormat.ShouldBe(MarkupFormat.ExtensibleMarkupLanguage);
        host.LastStaticContent.ShouldNotBeNull()
            .ShouldContain("<mi mathcolor=\"red\">a</mi>");
        CompiledFixtureNode dynamicMath = host.FindElements("mi").ShouldHaveSingleItem();
        dynamicMath.Bindings["mathcolor"].ShouldBe("blue");
        dynamicMath.DescendantText.ShouldBe("dynamic math");

        object instance = FindInstance(renderer, host, "MathStaticProbe");
        host.ResetOperationCounts();
        SetReferenceValue(instance, "Color", "green");
        SetReferenceValue(instance, "Label", "updated math");
        host.RunScheduledFlushes();

        host.StaticInsertionCount.ShouldBe(0);
        host.BindingPatchCount.ShouldBe(1);
        host.TextChangeCount.ShouldBe(1);
        host.MoveCount.ShouldBe(0);
        dynamicMath.Bindings["mathcolor"].ShouldBe("green");
        dynamicMath.DescendantText.ShouldBe("updated math");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void InteractionProbe_BindsPropertiesFallthroughEmitListenerAndNativeModel()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        List<IReadOnlyList<object?>> emissions = [];
        var invocation = new ComponentInvocation(
            arguments: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Configured",
                ["data-extra"] = "fallthrough",
            },
            listeners: new Dictionary<string, ComponentEventListener>(StringComparer.Ordinal)
            {
                ["changed"] = arguments => emissions.Add(arguments),
            });
        ComponentNode root = new(
            ComponentReference.ForName("InteractionProbe"),
            invocation);
        ApplicationContext application = CreateApplication(root, factory);
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        host.RunScheduledFlushes();
        CompiledFixtureNode main = host.FindElements("main").ShouldHaveSingleItem();
        main.Bindings["data-extra"].ShouldBe("fallthrough");
        host.Container.DescendantText.ShouldContain("Configuredraise");

        CompiledFixtureNode button = host.FindElements("button").ShouldHaveSingleItem();
        Delegate listener = button.Bindings.Values.OfType<Delegate>().ShouldHaveSingleItem();
        InvokeHostListener(listener);
        emissions.ShouldHaveSingleItem().ShouldBe(["Configured"]);

        CompiledFixtureNode input = host.FindElements("input").ShouldHaveSingleItem();
        ModelBinding model = input.ModelBinding
            ?? throw new InvalidOperationException(
                "The test directive did not observe the native v-model carrier.");
        model.Value.ShouldBe("initial");
        model.Setter("updated");
        object instance = FindInstance(renderer, host, "InteractionProbe");
        GetReferenceValue(instance, "Model").ShouldBe("updated");
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldContain("updated");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void DynamicSlot_StructuralChangeForcesTheChildRender()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        ComponentNode root = new(ComponentReference.ForName("SlotOwner"));
        ApplicationContext application = CreateApplication(root, factory);
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        host.RunScheduledFlushes();
        object owner = FindInstance(renderer, host, "SlotOwner");
        object child = FindInstance(renderer, host, "SlotChild");
        GetIntProperty(child, "RenderCount").ShouldBe(1);
        host.Container.DescendantText.ShouldContain("slot-first");
        fixtures.GeneratedSources
            .Single(pair => pair.Key.EndsWith(
                "SlotOwner.SingleFileComponent.g.cs",
                StringComparison.Ordinal))
            .Value
            .ShouldContain(
                "slotStability: (global::Assimalign.Viu.Components.SlotStability)2");

        SetReferenceValue(owner, "ShowSlot", false);
        host.RunScheduledFlushes();

        GetIntProperty(child, "RenderCount").ShouldBe(2);
        host.Container.DescendantText.ShouldNotContain("slot-first");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void VueContainerAndGeneratedHotReloadMarkers_RunEndToEnd()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        ComponentNode root = new(ComponentReference.ForName("VueProbe"));
        ApplicationContext application = CreateApplication(root, factory);
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldContain("3");
        string vueSource = fixtures.GeneratedSources
            .Single(pair => pair.Key.EndsWith(
                "VueProbe.SingleFileComponent.g.cs",
                StringComparison.Ordinal))
            .Value;
        vueSource.ShouldContain("static class Theme");
        vueSource.ShouldNotContain("Scope" + "Identifier");

        Type componentType = fixtures.GetComponentType("VueProbe");
        Type templateMarker = FindMarker(componentType, "Template");
        Type styleMarker = FindMarker(componentType, "Style");
        ComponentHotReload.Classify(componentType, [styleMarker])
            .ShouldBe(ComponentHotReloadChangeKind.StyleOnly);
        ComponentHotReload.Classify(componentType, [templateMarker])
            .ShouldBe(ComponentHotReloadChangeKind.Template);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void VueIsCast_CompiledFixture_RendersThroughRuntimeComponentResolver()
    {
        CompiledFixtureAssembly fixtures = CompiledFixtureAssembly.Instance;
        ComponentFactory factory = CreateFactory(fixtures);
        factory.Register(
            ComponentRegistration.Define(
                "runtime-widget",
                new ComponentContract(renderCacheSize: 0, displayName: "RuntimeWidget"),
                _ => _ => new ElementNode(
                    new QualifiedName("strong"),
                    children: [new TextNode("resolved at runtime")])));
        ComponentNode root = new(ComponentReference.ForName("VueRuntimeCastProbe"));
        ApplicationContext application = CreateApplication(root, factory);
        using var host = new CompiledFixtureHost();
        Renderer<CompiledFixtureNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        host.RunScheduledFlushes();

        host.Container.DescendantText.ShouldBe("resolved at runtime");
        renderer.GetMountedComponentViews(host.Container)
            .Select(view => view.Request.Component.RegisteredName)
            .ShouldContain("runtime-widget");
        fixtures.GeneratedSources
            .Single(pair => pair.Key.EndsWith(
                "VueRuntimeCastProbe.SingleFileComponent.g.cs",
                StringComparison.Ordinal))
            .Value
            .ShouldContain("ComponentReference.ForName(\"runtime-widget\")");
        renderer.Render(null, host.Container);
    }

    private static ComponentFactory CreateFactory(CompiledFixtureAssembly fixtures)
    {
        var factory = new ComponentFactory();
        fixtures.RegisterComponents(factory);
        return factory;
    }

    private static void RegisterRouterOutletDependencies(ComponentFactory factory)
    {
        factory.Register(RouterView.Registration);
        factory.Register(
            new ComponentRegistration(
                ComponentReference.ForName("RouterView"),
                RouterView.Registration.Contract,
                RouterView.Registration.Activator));
        factory.Register(
            new ComponentRegistration(
                ComponentReference.ForName("SynchronousTransition"),
                new ComponentContract(),
                static _ => new SynchronousTransitionComponent()));
    }

    private static void RegisterTransitionGroup(ComponentFactory factory)
    {
        // A DOM-less host supplies the same built-in aliases as BrowserApplication.
        factory.Register(new ComponentRegistration(ComponentReference.ForName("Transition"),
            Transition.Registration.Contract, Transition.Registration.Activator));
        factory.Register(
            new ComponentRegistration(
                ComponentReference.ForName("TransitionGroup"),
                TransitionGroup.Registration.Contract,
                TransitionGroup.Registration.Activator));
    }

    private static ApplicationContext CreateApplication(
        ComponentNode root,
        ComponentFactory factory,
        IServiceProvider? services = null,
        Action<Exception, ComponentContext?, string>? errorHandler = null,
        Action<string>? warningHandler = null)
    {
        var directives = new DirectiveRegistry(
        [
            new KeyValuePair<Type, IDirective>(
                typeof(VModelText),
                CompiledFixtureModelDirective.Instance),
            new KeyValuePair<Type, IDirective>(
                typeof(VModelCheckbox),
                CompiledFixtureModelDirective.Instance),
            new KeyValuePair<Type, IDirective>(
                typeof(VModelRadio),
                CompiledFixtureModelDirective.Instance),
            new KeyValuePair<Type, IDirective>(
                typeof(VModelSelect),
                CompiledFixtureModelDirective.Instance),
            new KeyValuePair<Type, IDirective>(
                typeof(VModelDynamic),
                CompiledFixtureModelDirective.Instance),
        ]);
        return new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = root,
                Components = factory,
                Directives = directives,
                Services = services,
                ErrorHandler = errorHandler,
                WarnHandler = warningHandler ?? (warning => throw new InvalidOperationException(warning)),
            });
    }

    private static object FindInstance(
        Renderer<CompiledFixtureNode> renderer,
        CompiledFixtureHost host,
        string componentName)
    {
        return renderer.GetMountedComponentViews(host.Container)
            .Select(view => view.Instance)
            .Single(instance => string.Equals(
                instance.GetType().Name,
                componentName,
                StringComparison.Ordinal));
    }

    private static void SetReferenceValue(
        object component,
        string fieldName,
        object value)
    {
        FieldInfo field = component.GetType().GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"The generated component does not expose field '{fieldName}'.");
        object reference = field.GetValue(component)
            ?? throw new InvalidOperationException(
                $"The generated component field '{fieldName}' is null.");
        PropertyInfo valueProperty = reference.GetType().GetProperty("Value")
            ?? throw new InvalidOperationException(
                $"The field '{fieldName}' is not a reactive reference.");
        valueProperty.SetValue(reference, value);
    }

    private static object? GetReferenceValue(object component, string fieldName)
    {
        FieldInfo field = component.GetType().GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"The generated component does not expose field '{fieldName}'.");
        object reference = field.GetValue(component)
            ?? throw new InvalidOperationException(
                $"The generated component field '{fieldName}' is null.");
        PropertyInfo valueProperty = reference.GetType().GetProperty("Value")
            ?? throw new InvalidOperationException(
                $"The field '{fieldName}' is not a reactive reference.");
        return valueProperty.GetValue(reference);
    }

    private static int GetIntProperty(object instance, string propertyName) =>
        (int)(instance.GetType().GetProperty(propertyName)!.GetValue(instance)
            ?? throw new InvalidOperationException(
                $"The generated component property '{propertyName}' is null."));

    private static void InvokeHostListener(Delegate listener)
    {
        ParameterInfo[] parameters = listener.Method.GetParameters();
        listener.DynamicInvoke(parameters.Length == 0 ? null : new object?[] { null });
    }

    private static Type FindMarker(Type componentType, string category) =>
        componentType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Single(type => type.Name.Contains(category, StringComparison.Ordinal)
                && type.Name.Contains("Marker", StringComparison.Ordinal));

    private sealed class RouterServiceProvider : IServiceProvider
    {
        private readonly ViuRouter _router;

        internal RouterServiceProvider(ViuRouter router)
        {
            _router = router;
        }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(ViuRouter) ? _router : null;
    }

    private sealed class SynchronousTransitionComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            var scope = new ComponentTransitionScope(context);
            var properties = new TransitionProperties
            {
                Mode = "out-in",
                OnLeave = static (_, complete) => complete(),
            };
            return _ => context.Bindings.Slots.TryGetValue(
                "default",
                out ComponentSlot? child)
                    ? scope.Attach(child, properties)
                    : null;
        }
    }

    private static void RunScheduledFlushes(Queue<Action> scheduledFlushes)
    {
        while (scheduledFlushes.Count > 0)
        {
            scheduledFlushes.Dequeue()();
        }
    }

    private static RendererOptions<int> CopyBrowserOptions(
        RendererOptions<int> source,
        Func<string, int> resolveTeleportTarget,
        Func<QualifiedName, int>? createElement = null) =>
        new()
        {
            Insert = source.Insert,
            Remove = source.Remove,
            CreateElement = createElement ?? source.CreateElement,
            CreateText = source.CreateText,
            CreateComment = source.CreateComment,
            SetText = source.SetText,
            ParentNode = source.ParentNode,
            NextSibling = source.NextSibling,
            PatchAttribute = source.PatchAttribute,
            ResolveTeleportTarget = resolveTeleportTarget,
            Commit = source.Commit,
            InsertStaticContent = source.InsertStaticContent,
            CreateHydrationReader = source.CreateHydrationReader,
        };
}
