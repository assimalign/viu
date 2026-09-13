using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Generators.FileRouting.Tests;

/// <summary>Authoring failures remain build diagnostics, never [RTR-7] runtime activation failures.</summary>
public sealed class FileRoutingDiagnosticsTests
{
    [Theory]
    [InlineData("One/Page.viu", "Two/Page.viu")]
    [InlineData("One/Page.viu", "Two/Page.vue")]
    [InlineData("Foo-Bar.viu", "Foo_Bar.viu")]
    [InlineData("Index.viu", "Blog/Index.viu")]
    public void Generate_DuplicateComponentIdentity_DiagnosesRenameBeforeRegistration(string first, string second)
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page(first), GeneratorTestHarness.Page(second));
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldContain("VIU2001");
        result.Diagnostics.Single(diagnostic => diagnostic.Id == "VIU2001").GetMessage().ShouldContain("rename one page file");
        result.Diagnostics.All(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeTrue();
        result.Diagnostics.All(diagnostic => diagnostic.Location.GetLineSpan().Path.StartsWith("/project/Pages/")).ShouldBeTrue();
        GeneratorTestHarness.Source(result).ShouldNotContain("new global::Assimalign.Viu.FileRouting.FileRouteDescriptor(");
    }

    [Fact]
    public void Generate_DuplicateStaticPathsAndDerivedNames_ReportsBoth()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("QuickStart.viu"), GeneratorTestHarness.Page("Quick-Start.viu"));
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldContain("VIU2002");
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldContain("VIU2003");
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldNotContain("VIU2001");
    }

    [Fact]
    public void Generate_DistinctPathsWithSameDerivedName_ReportsNameCollision()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Blog/[Slug].viu"), GeneratorTestHarness.Page("BlogSlug.viu",
            Route("path = \"/blog-slug\";")));
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldContain("VIU2003");
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldNotContain("VIU2002");
    }

    [Fact]
    public void Generate_MultipleEmptyChildren_ReportsPathCollisionBeyondPermittedParentChildPair()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Blog.viu"), GeneratorTestHarness.Page("Blog/Index.viu"),
            GeneratorTestHarness.Page("Blog/Home.viu", Route("path = \"\"; name = \"home\";")));
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldContain("VIU2002");
    }

    [Fact]
    public void Generate_DefaultChildExplicitNameMatchingParent_ReportsNameCollision()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Blog.viu"),
            GeneratorTestHarness.Page("Blog/Index.viu", Route("name = \"blog\";")));
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldContain("VIU2003");
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldNotContain("VIU2002");
    }

    [Theory]
    [InlineData("[...Rest]/Entry.viu")]
    [InlineData("[...Rest]/Index.viu")]
    public void Generate_CatchAllFolderFollowedByRouteSegment_ReportsPosition(string file)
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page(file));
        // An Index file contributes no route segment, so a catch-all folder followed by Index is terminal.
        if (file.EndsWith("Index.viu"))
        {
            result.Diagnostics.ShouldBeEmpty();
        }
        else
        {
            GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldContain("VIU2004");
        }
    }

    [Fact]
    public void Generate_CatchAllLayoutWithRelativeChild_ReportsPosition()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("[...Rest].viu"),
            GeneratorTestHarness.Page("[...Rest]/Entry.viu"));
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldContain("VIU2004");
    }

    [Theory]
    [InlineData("[].viu")]
    [InlineData("[[Name].viu")]
    [InlineData("[...].viu")]
    [InlineData("[Bad-Name].viu")]
    [InlineData("[2Name].viu")]
    [InlineData("Bad Name.viu")]
    [InlineData("Café.viu")]
    [InlineData("Bad--Name.viu")]
    [InlineData("-Name.viu")]
    [InlineData("Name_.viu")]
    [InlineData("Bad.Name.viu")]
    public void Generate_InvalidSegment_ReportsAuthoringDiagnostic(string file)
    {
        GeneratorTestHarness.DiagnosticIdentifiers(GeneratorTestHarness.Run(GeneratorTestHarness.Page(file))).ShouldContain("VIU2005");
    }

    [Theory]
    [InlineData("unknown = \"value\";")]
    [InlineData("path = \"/one\"; path = \"/two\";")]
    [InlineData("name = \"one\"; name = \"two\";")]
    [InlineData("path = '/one';")]
    [InlineData("path = \"/one\"")]
    [InlineData("path = \"/one\\n\";")]
    [InlineData("name = \"\";")]
    [InlineData("path = \"/one//two\";")]
    [InlineData("path = \"relative\";")]
    [InlineData("path = \"/one/\";")]
    [InlineData("path = \"/:name(\\\\d+)\";")]
    [InlineData("path = \"/:\";")]
    [InlineData("path = \"/:name-more\";")]
    public void Generate_MalformedRouteGrammar_ReportsDiagnostic(string content)
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Page.viu", Route(content)));
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldContain("VIU2006");
    }

    [Theory]
    [InlineData("@route\n")]
    [InlineData("@route {\n path = \"/one\";\n")]
    [InlineData("@route { path = \"/one\"; }\n")]
    [InlineData("@route lang=\"text\" {\n}\n")]
    [InlineData("@route {\n}\n@route {\n}\n")]
    public void Generate_MalformedViuRouteContainer_ReportsDiagnostic(string source)
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Page.viu", source));
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldContain("VIU2006");
    }

    [Theory]
    [InlineData("<route>path = \"/one\";")]
    [InlineData("<route lang=\"text\"></route>")]
    [InlineData("<route></route><route></route>")]
    public void Generate_MalformedVueRouteContainer_ReportsDiagnostic(string source)
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Page.vue", source));
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldContain("VIU2006");
    }

    [Fact]
    public void Generate_OverrideCatchAllNotLast_ReportsPositionDiagnostic()
    {
        var result = GeneratorTestHarness.Run(GeneratorTestHarness.Page("Page.viu", Route("path = \"/:rest(.*)*/tail\";")));
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldContain("VIU2004");
    }

    [Theory]
    [InlineData("ProjectDir", "")]
    [InlineData("ViuFileRoutingPagesDirectory", "")]
    [InlineData("ViuFileRoutingPagesDirectory", "/absolute")]
    [InlineData("ViuFileRoutingPagesDirectory", "C:\\absolute")]
    public void Generate_InvalidBuildProperties_ReportsConfiguration(string property, string value)
    {
        var result = GeneratorTestHarness.Driver(new AdditionalText[0], new Dictionary<string, string> { [property] = value })
            .RunGenerators(GeneratorTestHarness.Compilation()).GetRunResult().Results.Single();
        GeneratorTestHarness.DiagnosticIdentifiers(result).ShouldBe(new[] { "VIU2007" });
    }

    internal static string Route(string assignments) => "<template><div /></template>\n@route {\n" + assignments + "\n}\n";
}
