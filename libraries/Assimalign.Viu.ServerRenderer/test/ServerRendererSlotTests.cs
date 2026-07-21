using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Slot serialization through the runtime renderer — default, named, scoped, and fallback slots —
/// pinned to <c>renderSlot</c> and upstream <c>ssrRenderSlot</c>'s fragment-anchor wrapping.
/// </summary>
public class ServerRendererSlotTests
{
    private static InlineComponent SlotHost(string slotName) =>
        new((_, context) => () =>
            VirtualNodeFactory.Element("div", VirtualNodeFactory.RenderSlot(context.Slots, slotName)));

    [Fact]
    public async Task DefaultSlot_RendersProvidedContentInsideFragmentAnchors()
    {
        var child = SlotHost("default");
        var slots = new ComponentSlots();
        slots["default"] = _ => [VirtualNodeFactory.Element("span", "slotted")];
        var parent = new InlineComponent((_, _) => () => VirtualNodeFactory.Component(child, null, slots));

        var html = await Ssr.RenderAsync(parent);

        html.ShouldBe("<div><!--[--><span>slotted</span><!--]--></div>");
    }

    [Fact]
    public async Task NamedSlot_RendersMatchingContent()
    {
        var child = SlotHost("header");
        var slots = new ComponentSlots();
        slots["header"] = _ => [VirtualNodeFactory.Element("h1", "title")];
        var parent = new InlineComponent((_, _) => () => VirtualNodeFactory.Component(child, null, slots));

        var html = await Ssr.RenderAsync(parent);

        html.ShouldBe("<div><!--[--><h1>title</h1><!--]--></div>");
    }

    [Fact]
    public async Task ScopedSlot_ReceivesChildSuppliedScope()
    {
        // The child passes a scope object to the slot outlet; the parent-defined slot reads it.
        var child = new InlineComponent((_, context) => () =>
            VirtualNodeFactory.Element("div", VirtualNodeFactory.RenderSlot(context.Slots, "default", "scoped-value")));
        var slots = new ComponentSlots();
        slots["default"] = scope => [VirtualNodeFactory.Element("span", (string)scope!)];
        var parent = new InlineComponent((_, _) => () => VirtualNodeFactory.Component(child, null, slots));

        var html = await Ssr.RenderAsync(parent);

        html.ShouldBe("<div><!--[--><span>scoped-value</span><!--]--></div>");
    }

    [Fact]
    public async Task AbsentSlot_RendersFallbackContent()
    {
        var child = new InlineComponent((_, context) => () =>
            VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.RenderSlot(
                    context.Slots,
                    "default",
                    null,
                    () => [VirtualNodeFactory.Element("em", "fallback")])));
        // No slots passed at all.
        var parent = new InlineComponent((_, _) => () => VirtualNodeFactory.Component(child));

        var html = await Ssr.RenderAsync(parent);

        html.ShouldBe("<div><!--[--><em>fallback</em><!--]--></div>");
    }
}
