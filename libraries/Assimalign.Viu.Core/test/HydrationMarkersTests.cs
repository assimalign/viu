using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.Core.Tests;

public sealed class HydrationMarkersTests
{
    [Fact]
    public void MarkerConstants_SerializedWireVocabulary_UsesExactStableLiterals()
    {
        HydrationMarkers.FragmentStart.ShouldBe("<!--[-->");
        HydrationMarkers.FragmentEnd.ShouldBe("<!--]-->");
        HydrationMarkers.EmptyComment.ShouldBe("<!---->");
        HydrationMarkers.TeleportStart.ShouldBe("<!--teleport start-->");
        HydrationMarkers.TeleportEnd.ShouldBe("<!--teleport end-->");
        HydrationMarkers.TeleportAnchor.ShouldBe("<!--teleport anchor-->");
    }

    [Fact]
    public void HydrationNodeReader_HostAdapter_ExposesHostNeutralStructure()
    {
        ProbeNode child = new(HydrationNodeKind.Text, data: "content");
        ProbeNode root = new(
            HydrationNodeKind.Element,
            elementTag: "section",
            attributes: new Dictionary<string, string> { ["data-id"] = "42" },
            firstChild: child);
        child.Parent = root;
        ProbeHydrationNodeReader reader = new();

        reader.Kind(root).ShouldBe(HydrationNodeKind.Element);
        reader.ElementTag(root).ShouldBe("section");
        reader.Attribute(root, "data-id").ShouldBe("42");
        reader.FirstChild(root).ShouldBeSameAs(child);
        reader.ParentNode(child).ShouldBeSameAs(root);
        reader.Data(child).ShouldBe("content");
        reader.NextSibling(child).ShouldBeNull();
    }

    private sealed class ProbeHydrationNodeReader : HydrationNodeReader<ProbeNode>
    {
        public override HydrationNodeKind Kind(ProbeNode node) => node.Kind;

        public override ProbeNode? FirstChild(ProbeNode node) => node.FirstChild;

        public override ProbeNode? NextSibling(ProbeNode node) => node.NextSibling;

        public override ProbeNode? ParentNode(ProbeNode node) => node.Parent;

        public override string ElementTag(ProbeNode node) => node.ElementTag;

        public override string Data(ProbeNode node) => node.Data;

        public override string? Attribute(ProbeNode node, string name)
        {
            return node.Attributes.TryGetValue(name, out string? value) ? value : null;
        }
    }

    private sealed class ProbeNode
    {
        internal ProbeNode(
            HydrationNodeKind kind,
            string elementTag = "",
            string data = "",
            IReadOnlyDictionary<string, string>? attributes = null,
            ProbeNode? firstChild = null)
        {
            Kind = kind;
            ElementTag = elementTag;
            Data = data;
            Attributes = attributes ?? new Dictionary<string, string>();
            FirstChild = firstChild;
        }

        internal HydrationNodeKind Kind { get; }

        internal string ElementTag { get; }

        internal string Data { get; }

        internal IReadOnlyDictionary<string, string> Attributes { get; }

        internal ProbeNode? FirstChild { get; }

        internal ProbeNode? NextSibling { get; init; }

        internal ProbeNode? Parent { get; set; }
    }
}
