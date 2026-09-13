using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Router;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Pins packaged Router component consumption against the shipped metadata, including explicit nested
/// outlet depth and the opaque hand-authored registration contract ([V01.01.08.03.02], [RTR-4],
/// [CMP-26], and [SFC-USE-5]).
/// </summary>
public sealed class SingleFileComponentRouterUsageValidationTests
{
    private const string ProjectDirectory = "C:/proj";

    [Theory]
    [InlineData("<div><RouterView :depth=\"1\" /></div>")]
    [InlineData("<RouterLink to=\"/\">x</RouterLink>")]
    [InlineData("<RouterLink to=\"/\" replace activeClass=\"active\" exactActiveClass=\"exact\">x</RouterLink>")]
    public void PackagedRouterComponent_ValidUsage_CompilesWithoutWarningsOrErrors(string usage)
    {
        // [V01.01.08.03.02] Read the actual library as metadata, as a packaged consumer does.
        // Both Router components use registration contracts rather than [Parameter] properties.
        var compilation = GeneratorTestHarness.CreateCompilation(CreateRouterReferences());
        var file = new InMemoryAdditionalText(
            $"{ProjectDirectory}/NestedLayout.viu",
            "<template>\n    " + usage + "\n</template>\n");
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(file),
            "Demo",
            ProjectDirectory,
            emitHotReloadMetadata: "false");

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var consumerCompilation,
            out var diagnostics);

        diagnostics.ShouldBeEmpty();
        driver.GetRunResult().Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
        consumerCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is RoslynDiagnosticSeverity.Warning
                or RoslynDiagnosticSeverity.Error)
            .ShouldBeEmpty();
    }

    [Theory]
    [InlineData("RouterView")]
    [InlineData("RouterLink")]
    public void PackagedRouterComponent_ImperativeRegistration_RetainsIdentityWithUnknownParameters(
        string componentName)
    {
        // [CMP-26] The hand-authored registration carries arbitrary C#; [SFC-USE-5] preserves the
        // component's identity without interpreting an unreadable parameter surface as empty.
        var compilation = GeneratorTestHarness.CreateCompilation(CreateRouterReferences());
        var component = compilation.GetTypeByMetadataName("Assimalign.Viu.Router." + componentName);
        component.ShouldNotBeNull();
        component.DeclaringSyntaxReferences.ShouldBeEmpty();

        var declarations = ComponentSymbolCatalogReader.Read(compilation, CancellationToken.None);

        var declaration = declarations.Where(entry => entry.Name == componentName).ShouldHaveSingleItem();
        declaration.IsParameterSurfaceKnown.ShouldBeFalse();
    }

    private static IReadOnlyList<MetadataReference> CreateRouterReferences()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0
                && !Path.GetFileName(path).StartsWith("Assimalign.Viu.", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, typeof(object).Assembly.Location, StringComparison.OrdinalIgnoreCase))
            .Concat(
            [
                typeof(ComponentBase).Assembly.Location,
                typeof(Reactive).Assembly.Location,
                typeof(RouterView).Assembly.Location,
            ]);

        return paths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();
    }
}
