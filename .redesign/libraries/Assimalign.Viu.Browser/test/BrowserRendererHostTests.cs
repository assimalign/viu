using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins the per-render flush budget specified by [RND-HOST-4], [RND-IO-1], and [SCH-10..11].
public sealed class BrowserRendererHostTests
{
    [Fact]
    public void Render_SynchronousFlush_AppliesOneNonemptyCommandFrame()
    {
        List<int> frameLengths = [];
        var host = new BrowserRendererHost(
            (_, length) =>
            {
                frameLengths.Add(length);
                return [];
            });
        host.ObserveForeignHandle(100);
        Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
        var initial = new ElementNode(
            new QualifiedName("div"),
            [ElementBinding.Attribute(new QualifiedName("id"), "root")],
            [new TextNode("first")]);

        renderer.Render(initial, 100);

        host.InteropCallCount.ShouldBe(1);
        frameLengths.Count.ShouldBe(1);
        frameLengths[0].ShouldBeGreaterThan(0);

        var updated = new ElementNode(
            new QualifiedName("div"),
            [ElementBinding.Attribute(new QualifiedName("id"), "root")],
            [new TextNode("second")]);
        renderer.Render(updated, 100);

        host.InteropCallCount.ShouldBe(2);
        frameLengths.Count.ShouldBe(2);

        renderer.Render(null, 100);

        host.InteropCallCount.ShouldBe(3);
        frameLengths.Count.ShouldBe(3);
    }

    [Fact]
    public void Commit_EmptyFrame_IsIdempotent()
    {
        int applyCount = 0;
        var host = new BrowserRendererHost(
            (_, _) =>
            {
                applyCount++;
                return [];
            });

        host.Options.Commit!();
        host.Options.Commit!();

        applyCount.ShouldBe(0);
        host.InteropCallCount.ShouldBe(0);
    }

    [Fact]
    public void Activate_SecondHost_RejectsUntilFirstLeaseIsDisposed()
    {
        var firstHost = new BrowserRendererHost((_, _) => []);
        var secondHost = new BrowserRendererHost((_, _) => []);
        IDisposable firstActivation = firstHost.Activate();

        Action activateSecond = () => secondHost.Activate().Dispose();

        activateSecond.ShouldThrow<InvalidOperationException>();
        firstActivation.Dispose();
        firstActivation.Dispose();

        using IDisposable secondActivation = secondHost.Activate();
    }

    [Fact]
    public void Render_ScheduledFlush_CoalescesMultipleMutationsIntoOneCommandFrame()
    {
        Scheduler.Reset();
        Action? scheduledFlush = null;
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(
            flush => scheduledFlush = flush);
        try
        {
            var host = new BrowserRendererHost((_, _) => []);
            host.ObserveForeignHandle(200);
            Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
            renderer.Render(new TextNode("initial"), 200);
            host.InteropCallCount.ShouldBe(1);

            var updateJob = new SchedulerJob(
                () =>
                {
                    renderer.Render(new TextNode("intermediate"), 200);
                    renderer.Render(new TextNode("final"), 200);
                });
            Scheduler.QueueJob(updateJob);

            host.InteropCallCount.ShouldBe(1);
            scheduledFlush.ShouldNotBeNull();
            scheduledFlush();

            host.InteropCallCount.ShouldBe(2);
            Scheduler.IsFlushPending.ShouldBeFalse();
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    [Theory]
    [InlineData("div", null, null)]
    [InlineData("svg", null, "svg")]
    [InlineData("path", "http://www.w3.org/2000/svg", "svg")]
    [InlineData("foreignObject", null, "svg")]
    [InlineData("math", null, "mathml")]
    [InlineData("mi", "http://www.w3.org/1998/Math/MathML", "mathml")]
    public void CreateElement_QualifiedName_EncodesHostOwnedNamespace(
        string localName,
        string? namespaceName,
        string? expectedNamespace)
    {
        byte[]? appliedFrame = null;
        var host = new BrowserRendererHost(
            (frame, length) =>
            {
                appliedFrame = frame.AsSpan(0, length).ToArray();
                return [];
            });

        _ = host.Options.CreateElement(
            new QualifiedName(localName, namespaceName));
        host.Options.Commit!();

        byte[] frame = appliedFrame
            ?? throw new InvalidOperationException("The command frame was not applied.");
        frame[14].ShouldBe((byte)1);
        ReadCreateElementNamespace(frame).ShouldBe(expectedNamespace);
    }

    [Fact]
    public void CreateElement_UnknownNamespace_RejectsBeforeCommit()
    {
        var host = new BrowserRendererHost((_, _) => []);

        Action create = () => host.Options.CreateElement(
            new QualifiedName("widget", "urn:unknown"));

        create.ShouldThrow<NotSupportedException>();
        host.Options.Commit!();
        host.InteropCallCount.ShouldBe(0);
    }

    private static string? ReadCreateElementNamespace(byte[] frame)
    {
        const int namespaceReferenceOffset = 23;
        int namespaceIndex = BinaryPrimitives.ReadInt32LittleEndian(
            frame.AsSpan(namespaceReferenceOffset, sizeof(int)));
        if (namespaceIndex < 0)
        {
            return null;
        }

        int cursor = BinaryPrimitives.ReadInt32LittleEndian(
            frame.AsSpan(10, sizeof(int)));
        int stringCount = ReadInt32(frame, ref cursor);
        for (int index = 0; index < stringCount; index++)
        {
            int byteCount = ReadInt32(frame, ref cursor);
            string value = Encoding.UTF8.GetString(
                frame.AsSpan(cursor, byteCount));
            cursor += byteCount;
            if (index == namespaceIndex)
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"Namespace string index '{namespaceIndex}' is outside the command frame string table.");
    }

    private static int ReadInt32(byte[] frame, ref int cursor)
    {
        int value = BinaryPrimitives.ReadInt32LittleEndian(
            frame.AsSpan(cursor, sizeof(int)));
        cursor += sizeof(int);
        return value;
    }
}
