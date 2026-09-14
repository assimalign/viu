using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;

using Assimalign.Viu.Compiler.SingleFileComponent;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Compiles generated sibling-layout declarations against real runtime references, pinning type,
/// hint, registration, diagnostic, and incremental identity under <c>[SFC-CG-5]</c>,
/// <c>[SFC-CG-10]</c>, and <c>[V01.01.06.16]</c>.
/// </summary>
public sealed class SingleFileComponentSiblingIdentityTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";
    private const string Source = "<template><div>content</div></template>\n";

    [Theory]
    [InlineData(".viu", ".viu")]
    [InlineData(".viu", ".vue")]
    [InlineData(".vue", ".viu")]
    [InlineData(".vue", ".vue")]
    public void Generate_SiblingContainers_CompileWithUnchangedHintsAndRegistrationNames(
        string layoutExtension,
        string childExtension)
    {
        // [SFC-CG-10] Only the conflicting type moves; component tags still bind by [CMP-6] name.
        InMemoryAdditionalText layout = File("Pages/Blog" + layoutExtension,
            "<template><section><Entry /></section></template>\n");
        InMemoryAdditionalText child = File("Pages/Blog/Entry" + childExtension);
        GeneratorRunResult result = Generate(ImmutableArray.Create<AdditionalText>(layout, child));

        result.Diagnostics.ShouldBeEmpty();
        ComponentSource(result, "Pages.Blog.SingleFileComponent.g.cs")
            .ShouldContain("namespace Demo.Pages.GeneratedComponents\n{", Case.Sensitive);
        ComponentSource(result, "Pages.Blog.SingleFileComponent.g.cs")
            .ShouldContain("ComponentReference.ForName(\"Blog\")");
        ComponentSource(result, "Pages.Blog.SingleFileComponent.g.cs")
            .ShouldContain("ComponentReference.ForName(\"Entry\")");
        ComponentSource(result, "Pages.Blog.Entry.SingleFileComponent.g.cs")
            .ShouldContain("namespace Demo.Pages.Blog\n{", Case.Sensitive);
        result.GeneratedSources.Single(source => source.HintName == SingleFileComponentAssemblyEmitter.HintName)
            .SourceText.ToString().ShouldContain("global::Demo.Pages.GeneratedComponents.Blog.");
    }

    [Fact]
    public void Generate_AncestorAndNestedLayouts_MoveEachConflictingTypeOnly()
    {
        GeneratorRunResult result = Generate(ImmutableArray.Create<AdditionalText>(
            File("Pages/Blog.viu"), File("Pages/Blog/Archive.vue"),
            File("Pages/Blog/Archive/Entry.viu"), File("Views/Panel.viu")));

        result.Diagnostics.ShouldBeEmpty();
        ComponentSource(result, "Pages.Blog.SingleFileComponent.g.cs")
            .ShouldContain("namespace Demo.Pages.GeneratedComponents\n{", Case.Sensitive);
        ComponentSource(result, "Pages.Blog.Archive.SingleFileComponent.g.cs")
            .ShouldContain("namespace Demo.Pages.Blog.GeneratedComponents\n{", Case.Sensitive);
        ComponentSource(result, "Pages.Blog.Archive.Entry.SingleFileComponent.g.cs")
            .ShouldContain("namespace Demo.Pages.Blog.Archive\n{", Case.Sensitive);
        ComponentSource(result, "Views.Panel.SingleFileComponent.g.cs")
            .ShouldContain("namespace Demo.Views\n{", Case.Sensitive);
    }

    [Theory]
    [InlineData("class", "class", "@class")]
    [InlineData("Foo-Bar", "Foo_Bar", "Foo_Bar")]
    [InlineData("123", "123", "_123")]
    public void Generate_SanitizedSiblingIdentity_UsesCSharpIdentifiersAndKeepsOriginalHints(
        string fileBaseName,
        string directoryName,
        string className)
    {
        InMemoryAdditionalText layout = File("Pages/" + fileBaseName + ".viu");
        SingleFileComponentName original = SingleFileComponentNameResolver.Resolve(
            layout.Path, ProjectDirectory, RootNamespace);
        GeneratorRunResult result = Generate(ImmutableArray.Create<AdditionalText>(
            layout, File("Pages/" + directoryName + "/Entry.vue")));

        result.Diagnostics.ShouldBeEmpty();
        string source = ComponentSource(result, original.HintName);
        source.ShouldContain("namespace Demo.Pages.GeneratedComponents\n{", Case.Sensitive);
        source.ShouldContain("partial class " + className);
        source.ShouldContain("ComponentReference.ForName(\"" + className.TrimStart('@') + "\")");
    }

    [Fact]
    public void Generate_DirectoryDiffersOnlyByCase_KeepsOriginalTypeIdentity()
    {
        GeneratorRunResult result = Generate(ImmutableArray.Create<AdditionalText>(
            File("Pages/Blog.viu"), File("Pages/blog/Entry.viu")));

        result.Diagnostics.ShouldBeEmpty();
        ComponentSource(result, "Pages.Blog.SingleFileComponent.g.cs")
            .ShouldContain("namespace Demo.Pages\n{", Case.Sensitive);
        ComponentSource(result, "Pages.blog.Entry.SingleFileComponent.g.cs")
            .ShouldContain("namespace Demo.Pages.blog\n{", Case.Sensitive);
    }

    [Fact]
    public void Generate_GlobalNamespaceSibling_MovesLayoutIntoGeneratedNamespace()
    {
        GeneratorRunResult result = Generate(ImmutableArray.Create<AdditionalText>(
            File("Blog.viu"), File("Blog/Entry.viu")), rootNamespace: "");

        result.Diagnostics.ShouldBeEmpty();
        ComponentSource(result, "Blog.SingleFileComponent.g.cs")
            .ShouldContain("namespace GeneratedComponents\n{", Case.Sensitive);
        ComponentSource(result, "Blog.Entry.SingleFileComponent.g.cs")
            .ShouldContain("namespace Blog\n{", Case.Sensitive);
    }

    [Fact]
    public void Generate_ShadowedCompatibilityLayout_DoesNotCreateResidualIdentityError()
    {
        GeneratorRunResult result = Generate(ImmutableArray.Create<AdditionalText>(
            File("Pages/Blog.viu"), File("Pages/Blog.vue"), File("Pages/Blog/Entry.vue")));

        result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("VIU1004");
        ComponentSource(result, "Pages.Blog.SingleFileComponent.g.cs")
            .ShouldContain("namespace Demo.Pages.GeneratedComponents\n{", Case.Sensitive);
        result.GeneratedSources.Count(IsComponent).ShouldBe(2);
    }

    /// <summary>Residual generated type collisions and the exact files contributing to each error.</summary>
    public static TheoryData<string[], string[]> ResidualCollisions => new()
    {
        { ["Foo-Bar.viu", "Foo_Bar.vue"], ["Foo-Bar.viu", "Foo_Bar.vue"] },
        { ["Pages/Blog.viu", "Pages/Blog/Entry.viu", "Pages/GeneratedComponents.viu"],
            ["Pages/Blog.viu", "Pages/GeneratedComponents.viu"] },
        { ["Pages/Blog.viu", "Pages/Blog/Entry.viu", "Pages/GeneratedComponents/Blog.vue"],
            ["Pages/Blog.viu", "Pages/GeneratedComponents/Blog.vue"] },
        { ["Pages/Blog.viu", "Pages/Blog/Entry.viu", "Pages/GeneratedComponents/Blog/Other.vue"],
            ["Pages/Blog.viu", "Pages/GeneratedComponents/Blog/Other.vue"] },
    };

    [Theory]
    [MemberData(nameof(ResidualCollisions))]
    public void Generate_ResidualTypeCollision_ReportsLocatedErrorsAndOmitsConflictingDeclarations(
        string[] paths,
        string[] collidingPaths)
    {
        // [SFC-CG-10] Compilation remains valid: each collision is reported at its source component,
        // and unaffected components are still emitted instead of losing the entire generator run.
        GeneratorRunResult result = Generate(paths.Select(path => (AdditionalText)File(path))
            .Append(File("Unrelated.viu")).ToImmutableArray());

        result.Diagnostics.Length.ShouldBe(collidingPaths.Length);
        result.Diagnostics.Select(diagnostic => diagnostic.Location.GetLineSpan().Path)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ShouldBe(collidingPaths.Select(path => ProjectDirectory + "/" + path)
                .OrderBy(path => path, StringComparer.Ordinal));
        foreach (var diagnostic in result.Diagnostics)
        {
            diagnostic.Id.ShouldBe("VIU1005");
            diagnostic.Severity.ShouldBe(RoslynDiagnosticSeverity.Error);
            diagnostic.Location.Kind.ShouldBe(LocationKind.ExternalFile);
            diagnostic.Location.GetLineSpan().StartLinePosition.ShouldBe(new LinePosition(0, 0));
        }
        result.GeneratedSources.Count(IsComponent).ShouldBe(paths.Length - collidingPaths.Length + 1);
        ComponentSource(result, "Unrelated.SingleFileComponent.g.cs")
            .ShouldContain("partial class Unrelated");
    }

    [Fact]
    public void Generate_OutsideProjectDuplicateType_ReportsEachLocatedErrorWithoutCSharpCollision()
    {
        GeneratorRunResult result = Generate(ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("C:/linked/First/Button.viu", Source),
            new InMemoryAdditionalText("C:/linked/Second/Button.vue", Source)));

        result.Diagnostics.Length.ShouldBe(2);
        result.Diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "VIU1005"
            && diagnostic.Location.Kind == LocationKind.ExternalFile);
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Generate_ReversedInputOrder_PreservesSourcesAndRegistrationCatalog()
    {
        ImmutableArray<AdditionalText> files = ImmutableArray.Create<AdditionalText>(
            File("Pages/Blog.viu"), File("Pages/Blog/Entry.vue"), File("Views/Panel.viu"));
        GeneratorRunResult forward = Generate(files);
        GeneratorRunResult reversed = Generate(files.Reverse().ToImmutableArray());

        forward.Diagnostics.ShouldBeEmpty();
        reversed.Diagnostics.ShouldBeEmpty();
        DescribeSources(reversed).ShouldBe(DescribeSources(forward));
    }

    [Fact]
    public void Generate_ChildContentEdit_PreservesSiblingIdentityAndCachesUntouchedModels()
    {
        InMemoryAdditionalText layout = File("Pages/Blog.viu");
        InMemoryAdditionalText child = File("Pages/Blog/Entry.vue");
        InMemoryAdditionalText unrelated = File("Views/Panel.viu");
        GeneratorDriver driver = CreateDriver(ImmutableArray.Create<AdditionalText>(layout, child, unrelated));
        var compilation = GeneratorTestHarness.CreateCompilation(CreateReferences());
        driver = driver.RunGenerators(compilation);
        GeneratorRunResult original = driver.GetRunResult().Results.Single();

        driver = driver.ReplaceAdditionalText(child, File("Pages/Blog/Entry.vue",
            "<template><span>changed</span></template>\n")).RunGenerators(compilation);
        GeneratorRunResult edited = driver.GetRunResult().Results.Single();

        edited.Diagnostics.ShouldBeEmpty();
        ComponentSource(edited, "Pages.Blog.SingleFileComponent.g.cs")
            .ShouldBe(ComponentSource(original, "Pages.Blog.SingleFileComponent.g.cs"));
        ComponentSource(edited, "Views.Panel.SingleFileComponent.g.cs")
            .ShouldBe(ComponentSource(original, "Views.Panel.SingleFileComponent.g.cs"));
        var reasons = edited.TrackedSteps[SingleFileComponentGenerator.ModelTrackingName]
            .SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();
        reasons.Count(reason => reason == IncrementalStepRunReason.Cached).ShouldBe(2);
        reasons.Count(reason => reason == IncrementalStepRunReason.Modified).ShouldBe(1);
    }

    [Fact]
    public void Generate_AddThenRemoveLastDescendant_RestoresOriginalNamespaceWithoutChangingHint()
    {
        InMemoryAdditionalText layout = File("Pages/Blog.viu");
        InMemoryAdditionalText child = File("Pages/Blog/Entry.vue");
        GeneratorDriver driver = CreateDriver(ImmutableArray.Create<AdditionalText>(layout));
        var compilation = GeneratorTestHarness.CreateCompilation(CreateReferences());
        driver = driver.RunGenerators(compilation);
        string original = ComponentSource(driver.GetRunResult().Results.Single(), "Pages.Blog.SingleFileComponent.g.cs");
        original.ShouldContain("namespace Demo.Pages\n{", Case.Sensitive);

        driver = driver.AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(child)).RunGenerators(compilation);
        ComponentSource(driver.GetRunResult().Results.Single(), "Pages.Blog.SingleFileComponent.g.cs")
            .ShouldContain("namespace Demo.Pages.GeneratedComponents\n{", Case.Sensitive);

        driver = driver.RemoveAdditionalTexts(ImmutableArray.Create<AdditionalText>(child)).RunGenerators(compilation);
        GeneratorRunResult restored = driver.GetRunResult().Results.Single();
        restored.Diagnostics.ShouldBeEmpty();
        ComponentSource(restored, "Pages.Blog.SingleFileComponent.g.cs").ShouldBe(original);
    }

    private static InMemoryAdditionalText File(string relativePath, string source = Source)
        => new(ProjectDirectory + "/" + relativePath, source);

    private static GeneratorDriver CreateDriver(ImmutableArray<AdditionalText> files, string rootNamespace = RootNamespace)
        => GeneratorTestHarness.CreateDriver(files, rootNamespace, ProjectDirectory, emitHotReloadMetadata: "false");

    private static GeneratorRunResult Generate(ImmutableArray<AdditionalText> files, string rootNamespace = RootNamespace)
    {
        GeneratorDriver driver = CreateDriver(files, rootNamespace).RunGeneratorsAndUpdateCompilation(
            GeneratorTestHarness.CreateCompilation(CreateReferences()), out var compilation, out _);
        GeneratorRunResult result = driver.GetRunResult().Results.ShouldHaveSingleItem();

        result.Exception.ShouldBeNull();
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity is
            RoslynDiagnosticSeverity.Warning or RoslynDiagnosticSeverity.Error).ShouldBeEmpty();
        return result;
    }

    private static string ComponentSource(GeneratorRunResult result, string hintName)
        => result.GeneratedSources.Single(source => source.HintName == hintName).SourceText.ToString();

    private static bool IsComponent(GeneratedSourceResult source)
        => source.HintName.EndsWith(".SingleFileComponent.g.cs", StringComparison.Ordinal);

    private static string[] DescribeSources(GeneratorRunResult result)
        => result.GeneratedSources.OrderBy(source => source.HintName, StringComparer.Ordinal)
            .Select(source => source.HintName + "\n" + source.SourceText).ToArray();

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        IEnumerable<string> paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0
                && !Path.GetFileName(path).StartsWith("Assimalign.Viu.", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, typeof(object).Assembly.Location, StringComparison.OrdinalIgnoreCase))
            .Concat([typeof(ComponentBase).Assembly.Location, typeof(Reactive).Assembly.Location]);
        return paths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();
    }
}
