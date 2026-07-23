using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Pins template activation, setup, argument resolution, fallthrough attributes, and nested rendering.
/// </summary>
public class ServerRendererComponentTests
{
    [Fact]
    public async Task Template_Setup_RunsOnceAndRenders()
    {
        int setupCount = 0;
        InlineComponent component = new(_ =>
        {
            setupCount++;
            return () => TestTree.Element("div", "ready");
        });

        string html = await Ssr.RenderAsync(component);

        html.ShouldBe("<div>ready</div>");
        setupCount.ShouldBe(1);
    }

    [Fact]
    public async Task Template_DeclaredArgument_ResolvesFromRequest()
    {
        InlineComponent component = new(
            context => () => TestTree.Element(
                "h1",
                context.Arguments.Get<string>("title")!),
            parameters: [new ComponentParameter("title")]);

        string html = await Ssr.RenderAsync(
            component,
            TestTree.Arguments(("title", "Hi")));

        html.ShouldBe("<h1>Hi</h1>");
    }

    [Fact]
    public async Task Template_UndeclaredArguments_FallThroughOntoSingleRoot()
    {
        InlineComponent component = new(
            _ => () => TestTree.Element("div", "x"));

        string html = await Ssr.RenderAsync(
            component,
            TestTree.Arguments(("class", "box")));

        html.ShouldBe("<div class=\"box\">x</div>");
    }

    [Fact]
    public async Task Template_ComputedInSetup_EvaluatesExactlyOncePerRender()
    {
        int evaluationCount = 0;
        InlineComponent component = new(_ =>
        {
            IReactiveReference<int> count = Reactive.Reference(21);
            IReactiveReference<int> doubled = Reactive.Computed(() =>
            {
                evaluationCount++;
                return count.Value * 2;
            });
            return () => TestTree.Element("div", doubled.Value.ToString());
        });

        string html = await Ssr.RenderAsync(component);

        html.ShouldBe("<div>42</div>");
        evaluationCount.ShouldBe(1);
    }

    [Fact]
    public async Task Template_Nested_SerializesChildSubtree()
    {
        InlineComponent child = new(
            _ => () => TestTree.Element("span", "child"),
            name: "Child");
        InlineComponent parent = new(
            _ => () => ComponentTree.Element(
                "div",
                children: [child.Request()]));

        string html = await Ssr.RenderAsync(parent);

        html.ShouldBe("<div><span>child</span></div>");
    }

    [Fact]
    public async Task Template_MultiRootFragment_IsWrappedInHydrationAnchors()
    {
        InlineComponent component = new(
            _ => () => ComponentTree.Fragment(
                [
                    TestTree.Element("span", "a"),
                    TestTree.Element("span", "b"),
                ]));

        string html = await Ssr.RenderAsync(component);

        html.ShouldBe("<!--[--><span>a</span><span>b</span><!--]-->");
    }

    [Fact]
    public async Task Template_ServiceProvider_IsAvailableFromContext()
    {
        Greeting greeting = new("provided");
        TestServiceProvider services = new(
            new Dictionary<Type, object>
            {
                [typeof(Greeting)] = greeting,
            });
        InlineComponent component = new(context =>
        {
            Greeting resolved =
                (Greeting?)context.Services.GetService(typeof(Greeting))
                ?? throw new InvalidOperationException("Greeting was not supplied.");
            return () => TestTree.Element("div", resolved.Value);
        });

        string html = await Ssr.RenderAsync(
            component,
            services: services);

        html.ShouldBe("<div>provided</div>");
    }

    [Fact]
    public async Task Template_ScopeIdentifier_IsAppliedToRenderedElements()
    {
        InlineComponent component = new(
            _ => () => ComponentTree.Element(
                "section",
                children: [TestTree.Element("span", "content")]),
            scopeIdentifier: "data-v-report");

        string html = await Ssr.RenderAsync(component);

        html.ShouldBe(
            "<section data-v-report><span data-v-report>content</span></section>");
    }

    private sealed record Greeting(string Value);
}
