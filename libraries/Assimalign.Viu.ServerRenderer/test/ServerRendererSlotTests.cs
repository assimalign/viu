using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>Pins default, named, scoped, and fallback slot serialization.</summary>
public class ServerRendererSlotTests
{
    [Fact]
    public async Task DefaultSlot_RendersProvidedContentInsideFragmentAnchors()
    {
        InlineComponent child = SlotHost("default");
        Dictionary<string, ComponentSlot> slots = new()
        {
            ["default"] = _ => TestTree.Element("span", "slotted"),
        };
        InlineComponent parent = new(
            _ => () => child.Request(slots: slots));

        string html = await Ssr.RenderAsync(parent);

        html.ShouldBe("<div><!--[--><span>slotted</span><!--]--></div>");
    }

    [Fact]
    public async Task NamedSlot_RendersMatchingContent()
    {
        InlineComponent child = SlotHost("header");
        Dictionary<string, ComponentSlot> slots = new()
        {
            ["header"] = _ => TestTree.Element("h1", "title"),
        };
        InlineComponent parent = new(
            _ => () => child.Request(slots: slots));

        string html = await Ssr.RenderAsync(parent);

        html.ShouldBe("<div><!--[--><h1>title</h1><!--]--></div>");
    }

    [Fact]
    public async Task ScopedSlot_ReceivesChildSuppliedArguments()
    {
        InlineComponent child = new(context =>
        {
            return () => ComponentTree.Element(
                "div",
                children:
                [
                    ComponentTree.Fragment(
                        [
                            RenderHelpers._renderSlot(
                                context.Slots,
                                "default",
                                TestTree.Arguments(("value", "scoped-value"))),
                        ]),
                ]);
        });
        Dictionary<string, ComponentSlot> slots = new()
        {
            ["default"] = arguments =>
                TestTree.Element("span", arguments.Get<string>("value")!),
        };
        InlineComponent parent = new(
            _ => () => child.Request(slots: slots));

        string html = await Ssr.RenderAsync(parent);

        html.ShouldBe("<div><!--[--><span>scoped-value</span><!--]--></div>");
    }

    [Fact]
    public async Task AbsentSlot_RendersFallbackContent()
    {
        InlineComponent child = new(context =>
        {
            return () => ComponentTree.Element(
                "div",
                children:
                [
                    ComponentTree.Fragment(
                        [
                            RenderHelpers._renderSlot(
                                context.Slots,
                                "default",
                                fallback: () => [TestTree.Element("em", "fallback")]),
                        ]),
                ]);
        });
        InlineComponent parent = new(_ => () => child.Request());

        string html = await Ssr.RenderAsync(parent);

        html.ShouldBe("<div><!--[--><em>fallback</em><!--]--></div>");
    }

    private static InlineComponent SlotHost(string slotName)
    {
        return new InlineComponent(context =>
        {
            return () => ComponentTree.Element(
                "div",
                children:
                [
                    ComponentTree.Fragment(
                        [RenderHelpers._renderSlot(context.Slots, slotName)]),
                ]);
        });
    }
}
