using System;
using System.Buffers.Binary;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Browser.Tests;

// Unit-level coverage of the command-buffer encoder ([V01.01.04.05]): every opcode round-trips
// through encode -> finalize -> decode to the same DOM the ops describe, the frame header is
// well-formed, strings are interned, handles are pre-allocated from the .NET counter, and a version
// mismatch is rejected loudly. The full renderer differential run is in
// CommandBufferDifferentialTests; this file pins the wire format itself.
public sealed class DomCommandBufferTests
{
    [Fact]
    public void EveryOpcode_RoundTripsThroughEncodeAndDecode_ToTheSameDom()
    {
        var expected = new InMemoryHandleDom();
        var buffer = new DomCommandBuffer();

        // Pre-allocate handles from the buffer so the "expected" DOM and the decoded DOM agree on ids.
        var root = buffer.AllocateHandle();
        expected.CreateElementAs(root, "div", null);
        buffer.WriteCreateElement(root, "div", null);

        var svg = buffer.AllocateHandle();
        expected.CreateElementAs(svg, "svg", "svg");
        buffer.WriteCreateElement(svg, "svg", "svg");

        var text = buffer.AllocateHandle();
        expected.CreateTextAs(text, "hello");
        buffer.WriteCreateText(text, "hello");

        var comment = buffer.AllocateHandle();
        expected.CreateCommentAs(comment, "note");
        buffer.WriteCreateComment(comment, "note");

        foreach (var (child, anchor) in new[] { (text, 0), (svg, text), (comment, 0) })
        {
            expected.Insert(root, child, anchor);
            buffer.WriteInsert(root, child, anchor);
        }

        expected.SetText(text, "world");
        buffer.WriteSetText(text, "world");
        expected.SetAttribute(root, "id", "app");
        buffer.WriteSetAttribute(root, "id", "app");
        expected.RemoveAttribute(root, "id");
        buffer.WriteRemoveAttribute(root, "id");
        expected.SetXlinkAttribute(svg, "xlink:href", "#x");
        buffer.WriteSetXlinkAttribute(svg, "xlink:href", "#x");
        expected.RemoveXlinkAttribute(svg, "xlink:href");
        buffer.WriteRemoveXlinkAttribute(svg, "xlink:href");
        expected.SetClassName(root, "row active");
        buffer.WriteSetClassName(root, "row active");
        expected.SetStringProperty(root, "innerHTML", "<b>x</b>");
        buffer.WriteSetStringProperty(root, "innerHTML", "<b>x</b>");
        expected.SetBooleanProperty(root, "hidden", true);
        buffer.WriteSetBooleanProperty(root, "hidden", true);
        expected.SetValueGuarded(root, "typed");
        buffer.WriteSetValueGuarded(root, "typed");
        expected.SetStyleText(root, "color:red");
        buffer.WriteSetStyleText(root, "color:red");
        expected.SetStyleProperty(root, "color", "blue", important: true);
        buffer.WriteSetStyleProperty(root, "color", "blue", important: true);
        expected.RemoveStyleProperty(root, "color");
        buffer.WriteRemoveStyleProperty(root, "color");
        expected.AddEventListener(root, "click", once: true, capture: false, passive: true);
        buffer.WriteAddEventListener(root, "click", once: true, capture: false, passive: true);
        expected.RemoveEventListener(root, "click", capture: false);
        buffer.WriteRemoveEventListener(root, "click", capture: false);

        var actual = new InMemoryHandleDom();
        var length = buffer.FinalizeFrame();
        CommandBufferDecoder.Apply(buffer.BackingArray, length, actual);

        actual.Serialize(root).ShouldBe(expected.Serialize(root));
    }

    [Fact]
    public void FinalizeFrame_WritesAWellFormedVersionedHeader()
    {
        var buffer = new DomCommandBuffer();
        var handle = buffer.AllocateHandle();
        buffer.WriteCreateElement(handle, "div", null);
        buffer.WriteSetAttribute(handle, "id", "a");

        var length = buffer.FinalizeFrame();
        var frame = buffer.BackingArray.AsSpan(0, length);

        frame[0].ShouldBe(DomCommandBuffer.Magic);
        frame[1].ShouldBe(DomCommandBuffer.Version);
        BinaryPrimitives.ReadInt32LittleEndian(frame[2..]).ShouldBe(2); // opCount
        BinaryPrimitives.ReadInt32LittleEndian(frame[6..]).ShouldBe(2); // nextHandle after 1 allocation
        var stringTableOffset = BinaryPrimitives.ReadInt32LittleEndian(frame[10..]);
        stringTableOffset.ShouldBeGreaterThanOrEqualTo(DomCommandBuffer.HeaderSize);
        stringTableOffset.ShouldBeLessThan(length);
    }

    [Fact]
    public void RepeatedStrings_AreInternedOnce_InTheStringTable()
    {
        var buffer = new DomCommandBuffer();
        // "div" and "class" each appear many times; the table should hold each exactly once.
        for (var index = 0; index < 5; index++)
        {
            var handle = buffer.AllocateHandle();
            buffer.WriteCreateElement(handle, "div", null);
            buffer.WriteSetClassName(handle, "class");
        }

        var length = buffer.FinalizeFrame();
        var stringTableOffset = BinaryPrimitives.ReadInt32LittleEndian(buffer.BackingArray.AsSpan(10));
        var stringCount = BinaryPrimitives.ReadInt32LittleEndian(buffer.BackingArray.AsSpan(stringTableOffset));

        stringCount.ShouldBe(2); // "div", "class" — deduplicated regardless of op count
        length.ShouldBeGreaterThan(DomCommandBuffer.HeaderSize);
    }

    [Fact]
    public void AllocateHandle_DrawsAscendingIdsFromOne()
    {
        var buffer = new DomCommandBuffer();

        buffer.AllocateHandle().ShouldBe(1);
        buffer.AllocateHandle().ShouldBe(2);
        buffer.AllocateHandle().ShouldBe(3);
        buffer.NextHandle.ShouldBe(4);
    }

    [Fact]
    public void ObserveForeignHandle_KeepsTheCounterAboveJsAllocatedHandles()
    {
        var buffer = new DomCommandBuffer();
        buffer.AllocateHandle().ShouldBe(1);

        // A read (parentNode/nextSibling/querySelector) registered a foreign node JS-side as handle 7.
        buffer.ObserveForeignHandle(7);

        buffer.AllocateHandle().ShouldBe(8); // never collides with the foreign id
    }

    [Fact]
    public void ResetFrame_ClearsOpsButKeepsTheHandleCounter()
    {
        var buffer = new DomCommandBuffer();
        var first = buffer.AllocateHandle();
        buffer.WriteCreateElement(first, "div", null);
        buffer.FinalizeFrame();
        buffer.HasPendingOperations.ShouldBeTrue();

        buffer.ResetFrame();

        buffer.HasPendingOperations.ShouldBeFalse();
        buffer.OperationCount.ShouldBe(0);
        buffer.AllocateHandle().ShouldBe(2); // counter survived the reset (handles are process-global)
    }

    [Fact]
    public void Remove_ReturnsTheReleasedSubtreeHandles_FromTheApplyCall()
    {
        var dom = new InMemoryHandleDom();
        var buffer = new DomCommandBuffer();
        var parent = buffer.AllocateHandle();
        var child = buffer.AllocateHandle();
        var grandchild = buffer.AllocateHandle();
        buffer.WriteCreateElement(parent, "ul", null);
        buffer.WriteCreateElement(child, "li", null);
        buffer.WriteCreateText(grandchild, "x");
        buffer.WriteInsert(parent, child, 0);
        buffer.WriteInsert(child, grandchild, 0);
        buffer.WriteRemove(child);

        var released = CommandBufferDecoder.Apply(buffer.BackingArray, buffer.FinalizeFrame(), dom);

        // The single apply call reports every released handle in the removed subtree (child + text).
        released.ShouldBe([child, grandchild], ignoreOrder: false);
    }

    [Fact]
    public void FlipMoveOpcodes_RoundTripAsFloat64_InUpstreamWriteOrder()
    {
        var dom = new InMemoryHandleDom();
        var buffer = new DomCommandBuffer();
        var one = buffer.AllocateHandle();
        var two = buffer.AllocateHandle();
        buffer.WriteCreateElement(one, "li", null);
        buffer.WriteCreateElement(two, "li", null);

        // The FLIP write frame ([V01.01.04.07.03]): invert every moved child, one reflow barrier, then the
        // move class + transform clear — the exact upstream applyTranslation/forceReflow/moveClass order.
        buffer.WriteSetMoveTransform(one, 12.5, -7.25); // fractional deltas prove the float64 operand fidelity
        buffer.WriteSetMoveTransform(two, 0, 40);
        buffer.WriteForceReflow();
        buffer.WriteAddTransitionClass(one, "v-move");
        buffer.WriteClearMoveStyles(one);
        buffer.WriteAddTransitionClass(two, "v-move");
        buffer.WriteClearMoveStyles(two);

        CommandBufferDecoder.Apply(buffer.BackingArray, buffer.FinalizeFrame(), dom);

        // The single frame replays in order: transforms (float64 deltas intact), the reflow, then per child
        // the move class and the transform clear.
        dom.TransitionLog.ShouldBe(
        [
            $"transform:{one}:12.5,-7.25",
            $"transform:{two}:0,40",
            "reflow",
            "add:v-move",
            $"clear:{one}",
            "add:v-move",
            $"clear:{two}",
        ]);
        dom.ReflowCount.ShouldBe(1);
        // The move class stuck and the inverting transform was cleared so the element animates home.
        dom.TransitionClasses(one).ShouldContain("v-move");
        dom.MoveTransform(one).ShouldBeNull();
    }

    [Fact]
    public void Decoder_RejectsAFrameWithAMismatchedVersion()
    {
        var buffer = new DomCommandBuffer();
        var handle = buffer.AllocateHandle();
        buffer.WriteCreateElement(handle, "div", null);
        var length = buffer.FinalizeFrame();
        buffer.BackingArray[1] = 0x7F; // corrupt the version byte

        var dom = new InMemoryHandleDom();
        var exception = Record.Exception(() => CommandBufferDecoder.Apply(buffer.BackingArray, length, dom));

        exception.ShouldBeOfType<InvalidOperationException>();
        exception!.Message.ShouldContain("version mismatch");
    }
}
