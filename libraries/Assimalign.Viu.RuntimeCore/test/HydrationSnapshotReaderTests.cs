using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Shared;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.RuntimeCore.Tests;

// Snapshot-safety regression coverage for the hydration walker ([V01.01.07.03]). These run against a
// FROZEN reader (TestRenderer(snapshotSemantics: true)) that mirrors the browser's batched snapshot: an
// immutable pre-walk that never reflects an Insert/Remove/SetText, plus a double-remove that throws like the
// browser bridge's "unknown DOM handle". A walker that re-reads structure after mutating passes against the
// live-tree reader but breaks here — the exact bug class the browser hits. Live-reader coverage stays in
// HydrationTests.
public class HydrationSnapshotReaderTests : IDisposable
{
    private readonly TestRenderer _renderer = new(snapshotSemantics: true);
    private readonly TestNodeOperationLog _log;

    public HydrationSnapshotReaderTests()
    {
        Scheduler.Reset();
        BlockStack.Reset();
        _log = _renderer.OperationLog;
    }

    public void Dispose()
    {
        Scheduler.Reset();
        BlockStack.Reset();
    }

    [Fact]
    public void HandleMismatch_FragmentRange_ReadBeforeMutate_IsSnapshotSafe()
    {
        // Server is a multi-child fragment; the client wants a single <div>, so HandleMismatch must discard
        // the whole [ .. ] range. With an immutable snapshot, re-reading NextSibling(node) after each Remove
        // keeps returning the already-removed first child — the walker must collect the range first.
        var container = TestServerMarkup.Parse("<!--[--><span>x</span><span>y</span><!--]-->");
        var vnode = VirtualNodeFactory.Element("div", "z");
        using var warnings = new WarningCapture();

        _renderer.Hydrate(vnode, container);

        // Pre-fix: throws on the double-remove. Post-fix: the fragment range is gone and the client <div> is
        // mounted in its place — the tree converges.
        warnings.Messages.ShouldContain(message => message.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
        container.Children.Count.ShouldBe(1);
        ((TestElement)container.Children[0]).Tag.ShouldBe("div");
        ((TestText)((TestElement)container.Children[0]).Children[0]).Text.ShouldBe("z");
    }

    [Fact]
    public void HandleMismatch_NestedFragmentRange_IsSnapshotSafe()
    {
        // A nested [ .. ] inside the outer fragment: the removal range must honor nesting (locateClosingAnchor)
        // and still read the whole chain from the snapshot before removing.
        var container = TestServerMarkup.Parse("<!--[--><span>x</span><!--[--><i>n</i><!--]--><!--]-->");
        var vnode = VirtualNodeFactory.Element("div", "z");
        using var warnings = new WarningCapture();

        _renderer.Hydrate(vnode, container);

        warnings.Messages.ShouldContain(message => message.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
        container.Children.Count.ShouldBe(1);
        ((TestElement)container.Children[0]).Tag.ShouldBe("div");
    }

    [Fact]
    public void HydrateChildren_AdjacentText_AdoptsSplitDirectly_IsSnapshotSafe()
    {
        // Server renders two adjacent client text vnodes as ONE text node "ab". The walker splits it and must
        // adopt the created split node DIRECTLY (it is not in the snapshot), not resume via the frozen reader's
        // NextSibling — otherwise the split is orphaned, the second vnode adopts the original following sibling,
        // and the result drifts (e.g. "abb") plus a spurious mismatch warning.
        var container = TestServerMarkup.Parse("<div>ab</div>");
        var vnode = VirtualNodeFactory.Element(
            "div",
            null,
            new VirtualNode?[]
            {
                VirtualNodeFactory.Text("a"),
                VirtualNodeFactory.Text("b"),
            });
        using var warnings = new WarningCapture();

        _renderer.Hydrate(vnode, container);

        var div = (TestElement)container.Children[0];
        div.Children.Count.ShouldBe(2);
        ((TestText)div.Children[0]).Text.ShouldBe("a");
        ((TestText)div.Children[1]).Text.ShouldBe("b");
        warnings.Messages.ShouldBeEmpty();
    }

    [Fact]
    public void HydrateChildren_ThreeAdjacentText_PeeledInOrder_IsSnapshotSafe()
    {
        // Three adjacent text vnodes served as one "abc": the peel must produce "a","b","c" in order.
        var container = TestServerMarkup.Parse("<div>abc</div>");
        var vnode = VirtualNodeFactory.Element(
            "div",
            null,
            new VirtualNode?[]
            {
                VirtualNodeFactory.Text("a"),
                VirtualNodeFactory.Text("b"),
                VirtualNodeFactory.Text("c"),
            });
        using var warnings = new WarningCapture();

        _renderer.Hydrate(vnode, container);

        var div = (TestElement)container.Children[0];
        div.Children.Count.ShouldBe(3);
        ((TestText)div.Children[0]).Text.ShouldBe("a");
        ((TestText)div.Children[1]).Text.ShouldBe("b");
        ((TestText)div.Children[2]).Text.ShouldBe("c");
        warnings.Messages.ShouldBeEmpty();
    }

    // --- happy-path sanity under the frozen reader (proves frozen mode is correct for non-mutating walks) --

    [Fact]
    public void CleanElementHydration_UnderFrozenReader_AdoptsWithoutMutation()
    {
        var container = TestServerMarkup.Parse("<button>Click</button>");
        var clicks = 0;
        var vnode = VirtualNodeFactory.Element(
            "button",
            VirtualNodeFactory.Properties(("onClick", (Action)(() => clicks++))),
            "Click",
            PatchFlags.NeedHydration);

        _renderer.Hydrate(vnode, container);

        _log.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        _log.Count(TestNodeOperationType.Insert).ShouldBe(0);
        _log.Count(TestNodeOperationType.Remove).ShouldBe(0);
        var button = (TestElement)container.Children[0];
        ((Action)button.EventListeners["click"])();
        clicks.ShouldBe(1);
    }

    [Fact]
    public void CleanFragmentHydration_UnderFrozenReader_AdoptsAnchors()
    {
        var container = TestServerMarkup.Parse("<!--[--><span>a</span><span>b</span><!--]-->");
        var vnode = VirtualNodeFactory.Fragment(
            new VirtualNode?[] { VirtualNodeFactory.Element("span", "a"), VirtualNodeFactory.Element("span", "b") },
            null,
            PatchFlags.StableFragment);

        _renderer.Hydrate(vnode, container);

        _log.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        _log.Count(TestNodeOperationType.Insert).ShouldBe(0);
        _log.Count(TestNodeOperationType.Remove).ShouldBe(0);
        ((TestComment)vnode.El!).Text.ShouldBe("[");
        ((TestComment)vnode.Anchor!).Text.ShouldBe("]");
    }
}
