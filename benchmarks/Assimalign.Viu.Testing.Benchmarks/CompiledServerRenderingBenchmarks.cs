using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// Compares [V01.01.07.02]'s compiler-shaped direct-write server rendering with constructing and
/// traversing the equivalent immutable virtual tree. The virtual-tree path is the baseline, so
/// BenchmarkDotNet reports direct-write time and allocation ratios against the established runtime
/// serializer. Both paths include the same request-local application and renderer setup.
/// </summary>
[MemoryDiagnoser]
public class CompiledServerRenderingBenchmarks
{
    private static readonly ComponentFactory Components = new();
    private static readonly VirtualNode CompiledPlaceholder = new TextNode(string.Empty);
    private static readonly string[] ItemIdentifiers =
    [
        "1001",
        "1002",
        "1003",
        "1004",
        "1005",
        "1006",
        "1007",
        "1008",
    ];
    private static readonly string[] ItemNames =
    [
        "Alpha & Beta",
        "<Prototype>",
        "Gamma",
        "Delta",
        "Epsilon",
        "Zeta",
        "Eta",
        "Theta",
    ];
    private static readonly string[] ItemPrices =
    [
        "$12",
        "$18",
        "$21",
        "$25",
        "$29",
        "$34",
        "$38",
        "$42",
    ];

    /// <summary>
    /// Verifies before measurement that the compiler-shaped body and runtime serializer produce
    /// byte-identical HTML, including escaping and attribute ordering.
    /// </summary>
    [GlobalSetup]
    public void VerifyEquivalentOutput()
    {
        string virtualTreeOutput = VirtualTreeConstructionAndRenderingAsync()
            .GetAwaiter()
            .GetResult();
        string compiledOutput = CompiledDirectWriteRenderingAsync()
            .GetAwaiter()
            .GetResult();

        if (!string.Equals(virtualTreeOutput, compiledOutput, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The compiled and virtual-tree server-render benchmark fixtures are not byte-equivalent.");
        }
    }

    /// <summary>Constructs and serializes the representative catalog virtual tree.</summary>
    /// <returns>The completed WHATWG HTML serialization.</returns>
    [Benchmark(Baseline = true)]
    public Task<string> VirtualTreeConstructionAndRenderingAsync()
    {
        VirtualNode root = CreateVirtualTree();
        ServerRenderApplication application = new(root, Components);
        return ServerRender.RenderToStringAsync(application);
    }

    /// <summary>Executes the representative compiler-shaped direct-write body.</summary>
    /// <returns>The completed WHATWG HTML serialization.</returns>
    [Benchmark]
    public Task<string> CompiledDirectWriteRenderingAsync()
    {
        ServerRenderApplication application = new(CompiledPlaceholder, Components);
        return ServerRender.RenderCompiledToStringAsync(application, RenderCompiledAsync);
    }

    private static VirtualNode CreateVirtualTree()
    {
        var itemNodes = new VirtualNode[ItemNames.Length];
        for (int index = 0; index < itemNodes.Length; index++)
        {
            string itemClass = index == 0 ? "product featured" : "product";
            itemNodes[index] = Element(
                "li",
                [
                    ElementBinding.Attribute(new QualifiedName("class"), itemClass),
                    ElementBinding.Attribute(
                        new QualifiedName("data-item-id"),
                        ItemIdentifiers[index]),
                ],
                [
                    Element("span", children: [new TextNode(ItemNames[index])]),
                    Element("strong", children: [new TextNode(ItemPrices[index])]),
                ]);
        }

        return Element(
            "main",
            [
                ElementBinding.Attribute(new QualifiedName("id"), "catalog"),
                ElementBinding.Attribute(
                    new QualifiedName("aria-label"),
                    "Product & catalog"),
            ],
            [
                Element(
                    "header",
                    children:
                    [
                        Element("h1", children: [new TextNode("Catalog <W5.2>")]),
                        Element(
                            "p",
                            children:
                            [
                                new TextNode(
                                    "Compiled direct writes versus virtual trees."),
                            ]),
                    ]),
                Element(
                    "ul",
                    [ElementBinding.Attribute(new QualifiedName("class"), "products")],
                    itemNodes),
                Element("footer", children: [new TextNode("8 products")]),
            ]);
    }

    private static Task RenderCompiledAsync(SsrRenderState state)
    {
        state.Push(
            "<main id=\"catalog\" aria-label=\"Product &amp; catalog\"><header><h1>");
        state.Push(ServerRender.EscapeHtml("Catalog <W5.2>"));
        state.Push(
            "</h1><p>Compiled direct writes versus virtual trees.</p></header>" +
            "<ul class=\"products\">");

        for (int index = 0; index < ItemNames.Length; index++)
        {
            state.Push(index == 0
                ? "<li class=\"product featured\""
                : "<li class=\"product\"");
            state.Push(ServerRender.SsrRenderAttribute(
                "data-item-id",
                ItemIdentifiers[index]));
            state.Push("><span>");
            state.Push(ServerRender.EscapeHtml(ItemNames[index]));
            state.Push("</span><strong>");
            state.Push(ServerRender.EscapeHtml(ItemPrices[index]));
            state.Push("</strong></li>");
        }

        state.Push("</ul><footer>8 products</footer></main>");
        return Task.CompletedTask;
    }

    private static ElementNode Element(
        string name,
        IReadOnlyList<ElementBinding>? bindings = null,
        IReadOnlyList<VirtualNode>? children = null) =>
        new(new QualifiedName(name), bindings, children);
}
