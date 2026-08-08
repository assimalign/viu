using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

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

        SetReferenceValue(instance, "MemoMessage", "memo-blocked");
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldContain("memo-first");
        host.Container.DescendantText.ShouldNotContain("memo-blocked");

        SetReferenceValue(instance, "MemoKey", 2);
        host.RunScheduledFlushes();
        host.Container.DescendantText.ShouldContain("memo-blocked");
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

    private static ComponentFactory CreateFactory(CompiledFixtureAssembly fixtures)
    {
        var factory = new ComponentFactory();
        fixtures.RegisterComponents(factory);
        return factory;
    }

    private static ApplicationContext CreateApplication(
        ComponentNode root,
        ComponentFactory factory)
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
                WarnHandler = warning => throw new InvalidOperationException(warning),
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
}
