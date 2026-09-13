using Shouldly;
using Xunit;

namespace Assimalign.Viu.Generators.FileRouting.Tests;

public sealed class FileRoutingOverrideTests
{
    [Theory]
    [InlineData("Page.viu", "@route {\npath = \"/custom/:id\"; name = \"custom\";\n}\n")]
    [InlineData("Page.vue", "<route>path = \"/custom/:id\"; name = \"custom\";</route>")]
    public void Generate_CustomBlocksInBothContainers_ApplyPathNameAndParameterForwarding(string file, string content)
    {
        // .viu uses the [SFC-3] @ container; .vue uses the public [VUE-2] custom-block model.
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page(file, content));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"/custom/:id\", \"custom\", \"Page\", true)");
    }

    [Theory]
    [InlineData(":id", "id")]
    [InlineData(":id?", "id")]
    [InlineData(":items*", "items")]
    [InlineData(":items+", "items")]
    [InlineData(":rest(.*)*", "rest")]
    public void Generate_OverrideSupportedParameterGrammar_DerivesName(string segment, string name)
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Page.viu", FileRoutingDiagnosticsTests.Route("path = \"/custom/" + segment + "\";")));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("\"custom-" + name + "\", \"Page\", true");
    }

    [Fact]
    public void Generate_EmptyAndUnrelatedCustomBlocks_PreserveConventions()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Page.viu", "@docs {\nnotes\n}\n@route {\n}\n"));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"/page\", \"page\", \"Page\", false)");
    }

    [Fact]
    public void Generate_OverrideNameOnly_PreservesPathAndEscapesLiteralSafely()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Page.viu", FileRoutingDiagnosticsTests.Route("name = \"quote\\\" and slash\\\\\";")));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"/page\", \"quote\\\" and slash\\\\\", \"Page\", false)");
    }

    [Fact]
    public void Generate_LayoutPathOverride_FlowsIntoRelativeChildrenAndTheirDerivedNames()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Blog.viu", FileRoutingDiagnosticsTests.Route("path = \"/news/:section\";")),
            GeneratorTestHarness.Page("Blog/Article.viu"),
            GeneratorTestHarness.Page("Blog/Other.viu", FileRoutingDiagnosticsTests.Route("path = \"special\";")));
        result.Diagnostics.ShouldBeEmpty();
        var source = GeneratorTestHarness.Source(result);
        source.ShouldContain("(\"article\", \"news-section-article\", \"Article\", true)");
        source.ShouldContain("(\"special\", \"news-section-special\", \"Other\", true)");
    }

    [Fact]
    public void Generate_AbsoluteChildPathOverride_KeepsParentRecordButResetsResolvedPath()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Blog.viu"),
            GeneratorTestHarness.Page("Blog/Article.viu", FileRoutingDiagnosticsTests.Route("path = \"/outside/:id\";")));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"/outside/:id\", \"outside-id\", \"Article\", true)");
        GeneratorTestHarness.Source(result).ShouldContain("Layout Blog renders children");
    }

    [Fact]
    public void Generate_UniquelyNamedPageWithEmptyOverride_CanBeLayoutDefaultWithoutIndexIdentityCollision()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Index.viu"), GeneratorTestHarness.Page("Blog.viu"),
            GeneratorTestHarness.Page("Blog/Home.viu", FileRoutingDiagnosticsTests.Route("path = \"\";")));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"\", \"blog-index\", \"Home\", false)");
    }

    [Fact]
    public void Generate_RouteLikeContentInsideTemplateOrScript_DoesNotCreateOverride()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Page.viu", "<template><div>@route invalid</div></template>\n@script {\n // @route invalid\n}\n"));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"/page\", \"page\", \"Page\", false)");
    }
}
