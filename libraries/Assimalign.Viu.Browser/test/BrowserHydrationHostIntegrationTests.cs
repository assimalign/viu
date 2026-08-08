using System;
using System.Buffers.Binary;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins Browser's single-snapshot hydration host boundary [HYD-1..3].
public sealed class BrowserHydrationHostIntegrationTests
{
    [Fact]
    public void CreateHydrationReader_MaximumForeignHandle_AdvancesManagedAllocationPastSnapshot()
    {
        const string snapshot =
            "2 700 0 701 0 0 4:MAIN 0 701 700 0 0 0 3:DIV 0 ";
        byte[]? appliedFrame = null;
        int snapshotCount = 0;
        var host = new BrowserRendererHost(
            (frame, length) =>
            {
                appliedFrame = frame.AsSpan(0, length).ToArray();
                return [];
            },
            snapshotHydration: _ =>
            {
                snapshotCount++;
                return snapshot;
            });

        HydrationNodeReader<int> reader =
            host.Options.CreateHydrationReader!(700);
        int createdHandle = host.Options.CreateElement(
            new QualifiedName("span"));
        host.Options.Commit!();

        snapshotCount.ShouldBe(1);
        reader.FirstChild(700).ShouldBe(701);
        createdHandle.ShouldBe(702);
        byte[] frame = appliedFrame
            ?? throw new InvalidOperationException(
                "The command frame was not applied.");
        ReadFirstOperationHandle(frame).ShouldBe(702);
        ReadNextHandle(frame).ShouldBe(703);
    }

    [Fact]
    public void CreateHydrationReader_MalformedSnapshot_RejectsBeforeAnyCommandFrame()
    {
        var host = new BrowserRendererHost(
            (_, _) => [],
            snapshotHydration: _ => "not-an-integer ");

        Action createReader = () =>
        {
            _ = host.Options.CreateHydrationReader!(100);
        };

        createReader.ShouldThrow<FormatException>();
        host.InteropCallCount.ShouldBe(0);
    }

    private static int ReadFirstOperationHandle(byte[] frame)
    {
        const int firstOperationHandleOffset = 15;
        return BinaryPrimitives.ReadInt32LittleEndian(
            frame.AsSpan(firstOperationHandleOffset, sizeof(int)));
    }

    private static int ReadNextHandle(byte[] frame)
    {
        const int nextHandleOffset = 6;
        return BinaryPrimitives.ReadInt32LittleEndian(
            frame.AsSpan(nextHandleOffset, sizeof(int)));
    }
}
