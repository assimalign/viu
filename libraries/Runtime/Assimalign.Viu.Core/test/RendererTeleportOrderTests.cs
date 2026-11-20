using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins that teleports sharing one target keep the target's content in tree order across
/// reorders, removals, and disable toggles, while a lone teleport never pays for the ordering
/// pass. Specified by <c>[BLT-1]</c> through <c>[BLT-4]</c>.
/// </summary>
public sealed class RendererTeleportOrderTests
{
    [Fact]
    public void Render_TeleportsSharingATarget_FollowTreeOrderAfterReorderRemovalAndReenable()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#overlay");
        renderer.Render(Teleports(("a", false), ("b", false)), host.Container);
        TextOf(target).ShouldBe("ab");

        renderer.Render(Teleports(("b", false), ("a", false)), host.Container);
        TextOf(target).ShouldBe("ba");

        renderer.Render(Teleports(("b", false)), host.Container);
        TextOf(target).ShouldBe("b");

        renderer.Render(Teleports(("b", false), ("c", false)), host.Container);
        TextOf(target).ShouldBe("bc");

        renderer.Render(Teleports(("c", false), ("b", false)), host.Container);
        TextOf(target).ShouldBe("cb");

        // Disabling one teleport releases its target range; re-enabling it must restore ordering.
        renderer.Render(Teleports(("c", true), ("b", false)), host.Container);
        TextOf(target).ShouldBe("b");
        renderer.Render(Teleports(("b", false), ("c", false)), host.Container);
        TextOf(target).ShouldBe("bc");
        renderer.Render(Teleports(("c", false), ("b", false)), host.Container);
        TextOf(target).ShouldBe("cb");

        renderer.Render(null, host.Container);
        TextOf(target).ShouldBeEmpty();
        host.Container.Children.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Render_TeleportUpdateWithUnchangedPlacement_NeitherReadsSiblingsNorMovesChildren(bool isDisabled)
    {
        // [BLT-2] Children that already sit before their anchor stay put: an update that keeps the
        // teleport enabled (or disabled) at the same target issues no host moves and no reads.
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#overlay");
        renderer.Render(Teleports(("a", isDisabled)), host.Container);
        host.ResetOperationCounts();

        renderer.Render(
            new FragmentNode([new TeleportNode("#overlay", [new TextNode("a2")], isDisabled: isDisabled, key: "a")]),
            host.Container);

        TextOf(isDisabled ? host.Container : target).ShouldBe("a2");
        host.SiblingReadCount.ShouldBe(0);
        host.ParentReadCount.ShouldBe(0);
        host.MoveCount.ShouldBe(0);
        host.TextChangeCount.ShouldBe(1);
        renderer.Render(null, host.Container);
        TextOf(target).ShouldBeEmpty();
        host.Container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_TeleportToggledBetweenDisabledAndEnabled_MovesChildrenEachWay()
    {
        // [BLT-2] Toggling still relocates the children: into the origin when disabled, back before
        // the target anchor when re-enabled.
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#overlay");
        renderer.Render(Teleports(("a", false)), host.Container);
        RendererParityNode content = target.Children.Single(child => child.Kind == RendererParityNodeKind.Text);
        host.ResetOperationCounts();

        renderer.Render(Teleports(("a", true)), host.Container);

        TextOf(host.Container).ShouldBe("a");
        TextOf(target).ShouldBeEmpty();
        host.Container.Children.ShouldContain(content);
        host.MoveCount.ShouldBe(1);
        host.ResetOperationCounts();

        renderer.Render(Teleports(("a", false)), host.Container);

        TextOf(target).ShouldBe("a");
        TextOf(host.Container).ShouldBeEmpty();
        target.Children.ShouldContain(content);
        host.MoveCount.ShouldBe(1);
        renderer.Render(null, host.Container);
    }

    private static FragmentNode Teleports(params (string Text, bool IsDisabled)[] teleports)
    {
        var children = new List<VirtualNode>(teleports.Length);
        foreach ((string text, bool isDisabled) in teleports)
        {
            children.Add(new TeleportNode("#overlay", [new TextNode(text)], isDisabled: isDisabled, key: text));
        }

        return new FragmentNode(children);
    }

    private static string TextOf(RendererParityNode node) =>
        string.Concat(node.Children.Where(child => child.Kind == RendererParityNodeKind.Text).Select(child => child.Text));
}
