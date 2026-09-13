using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Generators.FileRouting.Tests;

/// <summary>Non-normative add-on conventions producing [RTR-1] records; [RTR-4] pins layout depth.</summary>
public sealed class FileRoutingGeneratorTests
{
    [Theory]
    [InlineData("QuickStart", "quick-start")]
    [InlineData("Blog", "blog")]
    [InlineData("XMLParser", "xml-parser")]
    [InlineData("HTTP2Server", "http2-server")]
    [InlineData("Version2", "version2")]
    [InlineData("V2Page", "v2-page")]
    [InlineData("API", "api")]
    [InlineData("My_Page", "my-page")]
    [InlineData("my-page", "my-page")]
    [InlineData("404", "404")]
    public void Generate_StaticSegments_UsePinnedOrdinalWordBoundaries(string fileName, string segment)
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page(fileName + ".viu"));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"/" + segment + "\", \"" + segment + "\",");
    }

    [Theory]
    [InlineData("[Slug]", ":slug", "slug", "_Slug_")]
    [InlineData("[[Slug]]", ":slug?", "slug", "__Slug__")]
    [InlineData("[...Rest]", ":rest(.*)*", "rest", "____Rest_")]
    [InlineData("[BlogSlug]", ":blogslug", "blogslug", "_BlogSlug_")]
    [InlineData("[Item_2]", ":item_2", "item_2", "_Item_2_")]
    public void Generate_DynamicSegments_ForwardParametersAndUseCompilerComponentIdentity(string fileName, string path, string name, string component)
    {
        // [CMP-26] The page declares [Parameter("slug")]; [RTR-2] keeps parameters typed at runtime.
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Blog/" + fileName + ".viu"));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"/blog/" + path + "\", \"blog-" + name + "\", \"" + component + "\", true)");
    }

    [Theory]
    [InlineData("Index.viu", "/", "index")]
    [InlineData("INDEX.viu", "/", "index")]
    [InlineData("Blog/Index.viu", "/blog", "blog")]
    [InlineData("Index/Guide.viu", "/index/guide", "index-guide")]
    public void Generate_IndexIsSpecialOnlyAsAFile_UsesDefaultPath(string page, string path, string name)
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page(page));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"" + path + "\", \"" + name + "\",");
    }

    [Fact]
    public void Generate_NestedLayouts_UsesRelativeChildrenAndExplicitDepthComments()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Blog.viu"),
            GeneratorTestHarness.Page("Blog/Article.viu"), GeneratorTestHarness.Page("Blog/Article/[Slug].viu"));
        result.Diagnostics.ShouldBeEmpty();
        var source = GeneratorTestHarness.Source(result);
        source.ShouldContain("(\"/blog\", \"blog\", \"Blog\", false,");
        source.ShouldContain("(\"article\", \"blog-article\", \"Article\", false,");
        source.ShouldContain("(\":slug\", \"blog-article-slug\", \"_Slug_\", true)");
        source.ShouldContain("Layout Blog renders children with <RouterView :depth=\"1\"/>");
        source.ShouldContain("Layout Article renders children with <RouterView :depth=\"2\"/>");
    }

    [Fact]
    public void Generate_LayoutDefaultChild_AllowsSameResolvedPathAndSuffixesChildName()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Blog.viu"), GeneratorTestHarness.Page("Blog/Index.viu"));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"\", \"blog-index\", \"Index\", false)");
    }

    [Fact]
    public void Generate_FolderBetweenLayouts_ContributesRelativeSegmentsOnly()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Blog.viu"), GeneratorTestHarness.Page("Blog/Archive/Entry.viu"));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"archive/entry\", \"blog-archive-entry\", \"Entry\", false)");
    }

    [Fact]
    public void Generate_LayoutSiblingNameComparison_IsOrdinalAndCaseSensitive()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Blog.viu"), GeneratorTestHarness.Page("blog/Entry.viu"));
        result.Diagnostics.ShouldBeEmpty();
        var source = GeneratorTestHarness.Source(result);
        source.ShouldContain("(\"/blog\", \"blog\", \"Blog\", false)");
        source.ShouldContain("(\"/blog/entry\", \"blog-entry\", \"Entry\", false)");
        source.ShouldNotContain("renders children");
    }

    [Fact]
    public void Generate_StaticChildUnderDynamicLayout_ForwardsInheritedParameters()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("[Slug].viu"), GeneratorTestHarness.Page("[Slug]/Details.viu"));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"details\", \"slug-details\", \"Details\", true)");
    }

    [Fact]
    public void Generate_UnrelatedAdditionalFilesAndLookalikeDirectories_AreIgnored()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Good.vue"),
            new InMemoryAdditionalText("/project/Components/Ignored.viu", "@route { wrong }"),
            new InMemoryAdditionalText("/project/PagesExtra/Ignored.viu", ""),
            new InMemoryAdditionalText("/project/Pages/Notes.txt", ""));
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("\"Good\", false");
        GeneratorTestHarness.Source(result).ShouldNotContain("Ignored");
        GeneratorTestHarness.Source(result).ShouldNotContain("Notes");
    }

    [Fact]
    public void Generate_CustomDirectory_NormalizesSeparatorsAndRelativeDotSegments()
    {
        var result = GeneratorTestHarness.Driver(new[] { new InMemoryAdditionalText("C:\\project\\Screens\\Start.vue", "") },
            new() { ["ProjectDir"] = "C:\\project\\", ["ViuFileRoutingPagesDirectory"] = "Other/../Screens/" })
            .RunGenerators(GeneratorTestHarness.Compilation()).GetRunResult().Results.Single();
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("(\"/start\", \"start\", \"Start\", false)");
    }

    [Fact]
    public void Generate_AbsentDirectory_EmitsEmptyTableWithoutFilesystemDiagnostic()
    {
        var result = GeneratorTestHarness.Run();
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("GeneratedViuFileRoutes");
        GeneratorTestHarness.Source(result).ShouldNotContain("new global::Assimalign.Viu.FileRouting.FileRouteDescriptor(");
    }

    [Fact]
    public void Generate_DisabledOrNotOptedIn_EmitsNothing()
    {
        foreach (var enabled in new[] { "false", "" })
        {
            var result = GeneratorTestHarness.Driver(new[] { GeneratorTestHarness.Page("Index.viu") },
                new() { ["ViuFileRoutingEnabled"] = enabled })
                .RunGenerators(GeneratorTestHarness.Compilation()).GetRunResult().Results.Single();
            result.Diagnostics.ShouldBeEmpty();
            result.GeneratedSources.ShouldBeEmpty();
        }
        var driver = GeneratorTestHarness.Driver(Array.Empty<AdditionalText>())
            .WithUpdatedAnalyzerConfigOptions(new InMemoryOptionsProvider(new Dictionary<string, string>()));
        var absent = driver.RunGenerators(GeneratorTestHarness.Compilation()).GetRunResult().Results.Single();
        absent.Diagnostics.ShouldBeEmpty();
        absent.GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Generate_Identity_HandlesDigitsKeywordsAndHyphensExactlyLikeComponentCatalog()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("404.viu"),
            GeneratorTestHarness.Page("class.viu"), GeneratorTestHarness.Page("Quick-start.viu"));
        result.Diagnostics.ShouldBeEmpty();
        var source = GeneratorTestHarness.Source(result);
        source.ShouldContain("\"_404\", false");
        source.ShouldContain("\"class\", false");
        source.ShouldContain("\"Quick_start\", false");
    }

    [Fact]
    public void Generate_InputOrder_DoesNotAffectOutputAndRecordsAreOrdinallySorted()
    {
        var pages = new[] { GeneratorTestHarness.Page("Zoo.viu"), GeneratorTestHarness.Page("Alpha.viu"), GeneratorTestHarness.Page("[Slug].viu") };
        var first = GeneratorTestHarness.Source(GeneratorTestHarness.Run(pages));
        var second = GeneratorTestHarness.Source(GeneratorTestHarness.Run(pages.Reverse().ToArray()));
        second.ShouldBe(first);
        first.IndexOf("\"/:slug\"", StringComparison.Ordinal).ShouldBeLessThan(first.IndexOf("\"/alpha\"", StringComparison.Ordinal));
        first.IndexOf("\"/alpha\"", StringComparison.Ordinal).ShouldBeLessThan(first.IndexOf("\"/zoo\"", StringComparison.Ordinal));
        first.ShouldStartWith("// <auto-generated/>\n#nullable enable\n\nnamespace Example.Application;");
        first.ShouldNotContain("using static");
        first.ShouldNotContain("RouteComponentFactory");
    }

    [Fact]
    public void Generate_EmptyRootNamespace_EmitsIntoGlobalNamespace()
    {
        var result = GeneratorTestHarness.Driver(Array.Empty<AdditionalText>(), new() { ["RootNamespace"] = "" })
            .RunGenerators(GeneratorTestHarness.Compilation()).GetRunResult().Results.Single();
        GeneratorTestHarness.Source(result).ShouldNotContain("namespace ");
    }

    [Fact]
    public void Generate_Output_ParsesAsCSharp()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Blog.viu"), GeneratorTestHarness.Page("Blog/[Slug].viu"));
        CSharpSyntaxTree.ParseText(GeneratorTestHarness.Source(result), new CSharpParseOptions(LanguageVersion.Preview))
            .GetDiagnostics().ShouldBeEmpty();
    }
}
