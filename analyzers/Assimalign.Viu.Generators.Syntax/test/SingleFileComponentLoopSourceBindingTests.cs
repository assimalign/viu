using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.ServerRenderer;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Pins declared collection element types through template binding and both render targets
/// ([SFC-6], [SFC-CG-2], and [V01.01.05.04.03]).
/// </summary>
public sealed class SingleFileComponentLoopSourceBindingTests
{
    /// <summary>Collection members and render targets that must preserve the loop alias type.</summary>
    public static TheoryData<string, string, bool> Sources
    {
        get
        {
            var sources = new TheoryData<string, string, bool>();
            foreach (bool serverRendering in new[] { false, true })
            {
                sources.Add(
                    "private IReadOnlyList<MarkdownSection> Sections { get; set; } = default!;\n" +
                    "partial void OnSetup() => Sections = new[] { new MarkdownSection() };",
                    "Sections", serverRendering);
                sources.Add(
                    "private IReadOnlyList<MarkdownSection> Sections { get; } = new[] { new MarkdownSection() };",
                    "Sections", serverRendering);
                sources.Add(
                    "private IReadOnlyList<MarkdownSection> Sections = new[] { new MarkdownSection() };",
                    "Sections", serverRendering);
                sources.Add(
                    "private IReadOnlyList<MarkdownSection> GetSections() => new[] { new MarkdownSection() };",
                    "GetSections()", serverRendering);
            }

            return sources;
        }
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void Loop_DeclaredCollectionMember_PreservesElementMemberAccess(
        string declaration,
        string expression,
        bool serverRendering)
    {
        // [V01.01.05.04.03] A settable property assigned during setup has the same element type
        // as a get-only property, a field, or a method returning that collection.
        CompileLoop(declaration, expression, serverRendering);
    }

    [Theory]
    [InlineData("Reference<IReadOnlyList<MarkdownSection>>", false)]
    [InlineData("Reference<IReadOnlyList<MarkdownSection>>", true)]
    [InlineData("IReactiveReference<IReadOnlyList<MarkdownSection>>", false)]
    [InlineData("IReactiveReference<IReadOnlyList<MarkdownSection>>", true)]
    [InlineData("ReactiveList<MarkdownSection>", false)]
    [InlineData("ReactiveList<MarkdownSection>", true)]
    public void Loop_ReactiveCollectionMember_PreservesElementMemberAccess(string type, bool serverRendering)
    {
        // [SFC-6] Known reference contracts unwrap their enumerable Value; reactive lists
        // are enumerable themselves and retain their declared type without an unwrap.
        CompileLoop(
            "private global::Assimalign.Viu.Reactivity." + type + " Sections { get; set; } = default!;",
            "Sections",
            serverRendering);
    }

    private static void CompileLoop(string declaration, string expression, bool serverRendering)
    {
        const string projectDirectory = "C:/proj";
        string source =
            "<template><div v-for=\"section in " + expression + "\">{{ section.Title }}</div></template>\n" +
            "@script {\nusing System.Collections.Generic;\n" + declaration + "\n}\n";
        var compilation = GeneratorTestHarness.CreateCompilation(
            CreateReferences(),
            "namespace Demo; public sealed class MarkdownSection { public string Title { get; } = \"Title\"; }");
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(projectDirectory + "/SectionsView.viu", source)),
            "Demo",
            projectDirectory,
            emitHotReloadMetadata: "false",
            serverRendering: serverRendering ? "true" : "false");

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var consumerCompilation, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        driver.GetRunResult().Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
        consumerCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is RoslynDiagnosticSeverity.Warning or RoslynDiagnosticSeverity.Error)
            .ShouldBeEmpty();
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0
                && !Path.GetFileName(path).StartsWith("Assimalign.Viu.", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, typeof(object).Assembly.Location, StringComparison.OrdinalIgnoreCase));

        return paths.Concat(
            [
                typeof(ComponentBase).Assembly.Location,
                typeof(Reactive).Assembly.Location,
                typeof(IComponentRenderScope).Assembly.Location,
                typeof(ServerRenderRegistry).Assembly.Location,
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
