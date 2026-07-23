using System;
using System.Runtime.Versioning;

using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser.Tests;

[SupportedOSPlatform("browser")]
public sealed class BrowserHydrationReaderTests
{
    private const string Snapshot =
        "4 "
        + "7 0 8 0 0 3:DIV 1 2:id 3:app "
        + "8 7 9 0 0 4:MAIN 1 10:data-count 1:2 "
        + "9 8 0 10 1 11:hello world "
        + "10 8 0 0 2 3:end ";

    [Fact]
    public void Snapshot_ProvidesStructureKindsDataAndAttributes()
    {
        BrowserHydrationReader reader = new(Snapshot);

        reader.Kind(7).ShouldBe(HydrationNodeKind.Element);
        reader.FirstChild(7).ShouldBe(8);
        reader.ParentNode(8).ShouldBe(7);
        reader.ElementTag(8).ShouldBe("MAIN");
        reader.Attribute(7, "id").ShouldBe("app");
        reader.Attribute(8, "data-count").ShouldBe("2");
        reader.Kind(9).ShouldBe(HydrationNodeKind.Text);
        reader.Data(9).ShouldBe("hello world");
        reader.NextSibling(9).ShouldBe(10);
        reader.Kind(10).ShouldBe(HydrationNodeKind.Comment);
        reader.Data(10).ShouldBe("end");
        reader.MaximumHandle.ShouldBe(10);
    }

    [Fact]
    public void UnknownHandle_ReturnsHostNeutralDefaults()
    {
        BrowserHydrationReader reader = new(Snapshot);

        reader.Kind(99).ShouldBe(HydrationNodeKind.Other);
        reader.FirstChild(99).ShouldBe(0);
        reader.NextSibling(99).ShouldBe(0);
        reader.ParentNode(99).ShouldBe(0);
        reader.ElementTag(99).ShouldBeEmpty();
        reader.Data(99).ShouldBeEmpty();
        reader.Attribute(99, "id").ShouldBeNull();
    }

    [Fact]
    public void BufferedReader_AdvancesAllocationPastEverySnapshotHandle()
    {
        int snapshotCount = 0;
        BufferedBrowserNodeOperations operations = new(
            static (_, _) => Array.Empty<int>(),
            static _ => 0,
            static _ => 0,
            static _ => 0,
            static (_, _, _, _) => (0, 0),
            _ =>
            {
                snapshotCount++;
                return Snapshot;
            });
        operations.ObserveForeignHandle(7);
        RendererOptions<int> options = operations.Create();

        HydrationNodeReader<int> reader =
            options.CreateHydrationReader!(7);
        int nextClientHandle =
            options.CreateElement("section", null);

        snapshotCount.ShouldBe(1);
        reader.FirstChild(7).ShouldBe(8);
        nextClientHandle.ShouldBe(11);
    }
}
