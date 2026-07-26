using System;
using System.Collections.Generic;
using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityStylesheetReferenceGraphBuilderTests
{
    [Fact]
    public void Build_NestedDuplicateAndCyclicReferences_ProducesFiniteDeterministicGraph()
    {
        const string rootCss =
            """
            @reference "./a.css";
            @reference "./a.css";
            """;
        var content = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["root.css\0./a.css"] = "@reference \"./b.css\";",
            ["a.css\0./b.css"] = "@reference \"./root.css\";",
            ["b.css\0./root.css"] = rootCss,
        };
        var identities = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["root.css\0./a.css"] = "a.css",
            ["a.css\0./b.css"] = "b.css",
            ["b.css\0./root.css"] = "root.css",
        };

        var graph = UtilityStylesheetReferenceGraphBuilder.Build(
            "root.css",
            rootCss,
            (referencingIdentity, specifier) =>
            {
                var key = referencingIdentity + "\0" + specifier;
                return content.TryGetValue(key, out var css)
                    ? new UtilityStylesheetReference(
                        referencingIdentity,
                        specifier,
                        identities[key],
                        css)
                    : null;
            });

        graph.References.Count.ShouldBe(3);
        graph.References[0].ReferencingSourceIdentity.ShouldBe("a.css");
        graph.References[1].ReferencingSourceIdentity.ShouldBe("b.css");
        graph.References[2].ReferencingSourceIdentity.ShouldBe("root.css");
    }

    [Fact]
    public void Build_CanceledTraversal_ThrowsOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        Should.Throw<OperationCanceledException>(
            () => UtilityStylesheetReferenceGraphBuilder.Build(
                "root.css",
                "@reference \"./a.css\";",
                (_, _) => null,
                cancellationSource.Token));
    }
}
