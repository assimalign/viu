using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.DevTools;

namespace Assimalign.Viu.DevTools.Client.Tests;

public sealed class ClientStoreTests
{
    [Fact]
    public void Tree_KeyedReorderAndUpdate_PreservesNodeAndDescendantIdentity()
    {
        // [DVT-14] component deltas mutate only the affected relationships.
        DevToolsComponentTreeStore tree = new();
        tree.Apply(new(1, null, 0, "Root", null));
        tree.Apply(new(2, 1, 0, "First", "first")); tree.Apply(new(3, 1, 1, "Second", "second"));
        DevToolsComponentNode first = tree.Nodes[2];
        tree.Reorder(2, 1); tree.Nodes[1].Children.Select(value => value.Identifier).ShouldBe([3, 2]);
        tree.Apply(new(2, 1, 1, "Changed", "first")); tree.Nodes[2].ShouldBeSameAs(first); first.Name.ShouldBe("Changed");
        tree.Remove(1); tree.Nodes.ShouldBeEmpty(); tree.Roots.ShouldBeEmpty();
    }

    [Fact]
    public void Tree_ChildMountsBeforeParent_AdoptsChildrenInStructuralOrder()
    {
        DevToolsComponentTreeStore tree = new();
        tree.Apply(new(2, 1, 0, "First", null)); tree.Apply(new(3, 1, 1, "Second", null));
        tree.Apply(new(1, null, 0, "Root", null));
        tree.Roots.Count.ShouldBe(1); tree.Nodes[1].Children.Select(value => value.Identifier).ShouldBe([2, 3]);
    }

    [Fact]
    public void Tree_NestedChildFirstMounts_KeepsEachParentsIndependentSiblingOrder()
    {
        DevToolsComponentTreeStore tree = new();
        tree.Apply(new(2, 1, 0, "First", null));
        tree.Apply(new(4, 3, 0, "Grandchild", null));
        tree.Apply(new(3, 1, 1, "Second", null));
        tree.Apply(new(1, null, 0, "Root", null));
        tree.Roots.Count.ShouldBe(1);
        tree.Nodes[1].Children.Select(value => value.Identifier).ShouldBe([2, 3]);
        tree.Nodes[3].Children.Single().Identifier.ShouldBe(4);
        tree.Remove(1);
        tree.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public void Timeline_WindowFiltersZoomAndCorrelation_FollowsTransitiveIdentityLinks()
    {
        DevToolsTimelineStore timeline = new(8);
        timeline.RegisterLayer(new("custom", "Custom", null));
        timeline.Apply(new TimelineEventPayload(1, 10, 5, "reactivity", "state.write", "Count", DependencyIdentifier: 7));
        timeline.Apply(new TimelineEventPayload(2, 20, 5, "scheduler", "flush.started", "Flush", EffectIdentifier: 9));
        timeline.Apply(new TimelineEventPayload(3, 30, 6, "components", "render.completed", "Counter", EffectIdentifier: 9, ComponentIdentifier: 2));
        timeline.Apply(new TimelineEventPayload(4, 40, 7, "custom", "custom", "Item", ComponentIdentifier: 2));
        timeline.Apply(new TimelineEventPayload(5, 50, 8, "custom", "custom", "Other"));
        timeline.GetRelatedSequences(1).Order().ShouldBe([1L, 2L, 3L, 4L]);
        timeline.GetWindow(0, 1, "custom", 35, 45).Single().Sequence.ShouldBe(4);
        timeline.GetWindow(1, 2).Select(value => value.Sequence).ShouldBe([2L, 3L]);
        for (int index = 0; index < 20; index++)
        {
            timeline.Apply(new TimelineDroppedPayload(1));
        }

        timeline.Gaps.Count.ShouldBe(8); timeline.Clear(); timeline.Layers.ShouldBeEmpty(); timeline.Gaps.ShouldBeEmpty();
    }
}
