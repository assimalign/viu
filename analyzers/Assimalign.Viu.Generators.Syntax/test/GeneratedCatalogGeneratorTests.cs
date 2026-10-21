using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Generators.Syntax.Tests;

public sealed class GeneratedCatalogGeneratorTests
{
    [Fact]
    public void AssembliesSharingRootNamespace_GenerateUnambiguousCatalogCalls()
    {
        // [CMP-36] The public metadata catalog identity must distinguish assemblies, not namespaces.
        MetadataReference[] references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).StartsWith("Assimalign.", StringComparison.Ordinal))
            .Concat([typeof(ComponentFactory).Assembly.Location, typeof(Assimalign.Viu.Scheduler).Assembly.Location,
                typeof(Assimalign.Viu.Reactivity.Reference<>).Assembly.Location])
            .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        CSharpCompilation Compile(string assemblyName, string componentName, MetadataReference[] metadata)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(assemblyName, references: metadata,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            GeneratorDriver driver = GeneratorTestHarness.CreateDriver(
                ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                    "C:/proj/" + componentName + ".viu", "<template><p>catalog</p></template>")),
                "SharedNamespace", "C:/proj");
            driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation generated, out var diagnostics);
            diagnostics.ShouldBeEmpty();
            generated.GetDiagnostics().Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .ShouldBeEmpty();
            return (CSharpCompilation)generated;
        }
        using MemoryStream image = new();
        Compile("SharedLibrary", "LibraryComponent", references).Emit(image).Success.ShouldBeTrue();
        CSharpCompilation application = Compile("SharedApplication", "App", references.Append(
            MetadataReference.CreateFromImage(image.ToArray())).ToArray());
        application.GetDiagnostics().Where(diagnostic => diagnostic.Id == "CS0436").ShouldBeEmpty();
    }

    [Fact]
    public void RegisteredNameOverride_ChangesMetadataAndRegistrationButPreservesTypeIdentity()
    {
        // [CMP-35] The standard C# constant needs no separate editor authoring syntax.
        GeneratorOutcome outcome = GeneratorTestHarness.Run("C:/proj/App.viu",
            "<template><p>app</p></template>\n@script {\n public const string RegisteredName = \"Acme.App\";\n}\n",
            "Demo", "C:/proj");
        outcome.Diagnostics.ShouldBeEmpty();
        string source = GeneratorTestHarness.GeneratedSource(outcome, "App.SingleFileComponent.g.cs");
        source.ShouldContain("partial class App");
        source.ShouldContain("ComponentMetadata(\"Acme.App\")");
        source.ShouldContain("ComponentReference.ForName(\"Acme.App\")");
        source.ShouldContain("IComponentDefinition.Registration => __ViuRegistration");
    }

    [Fact]
    public void DuplicateRegisteredNames_ReportLocatedConflictsAndOmitCatalogEntries()
    {
        const string source = "<template><p>app</p></template>\n@script {\n const string RegisteredName = \"same\";\n}\n";
        GeneratorOutcome outcome = GeneratorTestHarness.RunAll(
            [("C:/proj/First.viu", source), ("C:/proj/Second.viu", source)], "Demo", "C:/proj");
        outcome.Diagnostics.Count(diagnostic => diagnostic.Id == "VIU1005").ShouldBe(2);
        outcome.Diagnostics.Select(diagnostic => diagnostic.Location.GetLineSpan().Path)
            .ShouldBe(["C:/proj/First.viu", "C:/proj/Second.viu"], ignoreOrder: true);
    }

    [Fact]
    public void CatalogReference_DiscoveredFromMetadata_EmitsDirectRootedDelegate()
    {
        string source = """
            [assembly: Assimalign.Viu.Components.ComponentCatalog(typeof(Referenced.LibraryCatalog))]
            namespace Referenced;
            public static class LibraryCatalog
            {
                public static void Register(Assimalign.Viu.Components.ComponentFactory factory) { }
            }
            """;
        MetadataReference[] platformReferences = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        CSharpCompilation library = CSharpCompilation.Create("CatalogLibrary",
            [GeneratorTestHarness.Parse(source)], platformReferences.Append(
                MetadataReference.CreateFromFile(typeof(ComponentFactory).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using MemoryStream image = new();
        var emitted = library.Emit(image);
        emitted.Success.ShouldBeTrue(string.Join(Environment.NewLine, emitted.Diagnostics));
        GeneratorOutcome outcome = GeneratorTestHarness.RunAll(
            [("C:/proj/App.viu", "<template><p>app</p></template>")], "Demo", "C:/proj",
            platformReferences.Append(MetadataReference.CreateFromImage(image.ToArray())).ToArray());
        string aggregate = GeneratorTestHarness.GeneratedSource(outcome, "Viu.SingleFileComponentAssembly.g.cs");
        aggregate.ShouldContain("global::Referenced.LibraryCatalog.Register");
        aggregate.ShouldContain("CatalogLibrary, Version=0.0.0.0");
        aggregate.ShouldContain("ModuleInitializer");
        aggregate.ShouldNotContain("GetCustomAttributes");

        GeneratorDriver optedOut = GeneratorTestHarness.CreateDriver(ImmutableArray<AdditionalText>.Empty,
            "HostTests", "C:/proj", autoRegisterGeneratedCatalogs: "false");
        GeneratorRunResult optedOutResult = optedOut.RunGenerators(GeneratorTestHarness.CreateCompilation(
            platformReferences.Append(MetadataReference.CreateFromImage(image.ToArray())).ToArray()))
            .GetRunResult().Results.Single();
        // [CMP-36] A target-only metadata test host must not acquire runtime attribute dependencies.
        optedOutResult.GeneratedSources.ShouldBeEmpty();

        GeneratorDriver hostApplication = GeneratorTestHarness.CreateDriver(ImmutableArray<AdditionalText>.Empty,
            "HostApplication", "C:/proj", autoRegisterGeneratedCatalogs: null);
        CSharpCompilation hostCompilation = GeneratorTestHarness.CreateCompilation(
            platformReferences.Append(MetadataReference.CreateFromImage(image.ToArray())).ToArray())
            .WithOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        string hostAggregate = hostApplication.RunGenerators(hostCompilation).GetRunResult().Results.Single()
            .GeneratedSources.Single().SourceText.ToString();
        // A hosting SDK need not import Viu's SDK to receive executable-compilation aggregation.
        hostAggregate.ShouldContain("ModuleInitializer");
        hostAggregate.ShouldContain("global::Referenced.LibraryCatalog.Register");
    }

    [Fact]
    public void AutoRegistrationOptOut_PreservesCatalogAndMetadataWithoutModuleInitializer()
    {
        GeneratorDriver driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("C:/proj/App.viu", "<template><p>app</p></template>")),
            "Demo", "C:/proj", autoRegisterGeneratedCatalogs: "false");
        GeneratorRunResult result = driver.RunGenerators(GeneratorTestHarness.CreateCompilation()).GetRunResult().Results.Single();
        string aggregate = result.GeneratedSources.Single(source => source.HintName == "Viu.SingleFileComponentAssembly.g.cs").SourceText.ToString();
        aggregate.ShouldContain("ComponentCatalog(typeof(global::Demo.GeneratedViuComponents_");
        aggregate.ShouldContain("public static void Register(");
        aggregate.ShouldNotContain("ModuleInitializer");
    }
}
