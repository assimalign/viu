using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins the compiler-backed component contract seam used by template completion and hover.
/// </summary>
public class ScriptComponentContractTests
{
    [Fact]
    public void GetComponentContracts_SiblingSingleFileComponent_UsesProjectionDeclarations()
    {
        const string componentSource =
            "<template>\n" +
            "    <button>{{ Label }}</button>\n" +
            "</template>\n" +
            "@script {\n" +
            "    [Parameter(IsRequired = true)]\n" +
            "    public string Label { get; set; } = string.Empty;\n" +
            "}\n";
        var engine = new ScriptSemanticEngine();
        var context = ScriptSemanticFixture.CreateContext(
            new LanguageProjectSourceDocument(
                "C:/workspace/App/Button.viu",
                componentSource,
                IsComponent: true));

        var contracts = engine.GetComponentContracts(
            context,
            ScriptSemanticFixture.DocumentFilePath,
            "<template>\n  <Button />\n</template>\n",
            default);

        contracts.ShouldNotBeNull();
        var button = contracts!.Single(
            declaration => declaration.CompilerDeclaration.Name == "Button");
        var label = button.CompilerDeclaration.Parameters.Single();
        label.Name.ShouldBe("label");
        label.PropertyName.ShouldBe("Label");
        label.TypeText.ShouldBe("string");
        label.IsRequired.ShouldBeTrue();
        button.CompilerDeclaration.IsParameterSurfaceKnown.ShouldBeTrue();
    }

    [Fact]
    public void GetComponentContracts_RepeatedReadsReuseAndSiblingOrReferenceChangeInvalidates()
    {
        const string alphaSource =
            "<template><div /></template>\n" +
            "@script {\n" +
            "    [Parameter]\n" +
            "    public string Alpha { get; set; } = string.Empty;\n" +
            "}\n";
        var sibling = new LanguageProjectSourceDocument(
            "C:/workspace/App/Button.viu",
            alphaSource,
            IsComponent: true);
        var engine = new ScriptSemanticEngine();
        const string liveSource = "<template>\n  <Button />\n</template>\n";

        var first = engine.GetComponentContracts(
            ScriptSemanticFixture.CreateContext("stamp-1", sibling),
            ScriptSemanticFixture.DocumentFilePath,
            liveSource,
            default);
        var repeated = engine.GetComponentContracts(
            ScriptSemanticFixture.CreateContext("stamp-1", sibling),
            ScriptSemanticFixture.DocumentFilePath,
            liveSource,
            default);

        first.ShouldNotBeNull();
        repeated.ShouldNotBeNull();
        engine.ComponentContractBuildCount.ShouldBe(1);

        var betaSibling = sibling with
        {
            Text = alphaSource.Replace("Alpha", "Beta"),
        };
        var afterSiblingEdit = engine.GetComponentContracts(
            ScriptSemanticFixture.CreateContext("stamp-2", betaSibling),
            ScriptSemanticFixture.DocumentFilePath,
            liveSource,
            default);

        afterSiblingEdit.ShouldNotBeNull();
        afterSiblingEdit!
            .Single(declaration => declaration.CompilerDeclaration.Name == "Button")
            .CompilerDeclaration.Parameters
            .ShouldContain(parameter => parameter.PropertyName == "Beta");
        engine.ComponentContractBuildCount.ShouldBe(2);

        var changedReferences = ScriptSemanticFixture.ReferenceAssemblyPaths()
            .Concat([typeof(System.Text.Json.JsonDocument).Assembly.Location])
            .ToArray();
        var referenceChangedContext = new LanguageProjectContext(
            ScriptSemanticFixture.ProjectFilePath,
            ScriptSemanticFixture.ProjectDirectory,
            ScriptSemanticFixture.RootNamespace,
            changedReferences,
            new List<LanguageProjectSourceDocument> { betaSibling },
            ScriptSemanticFixture.PreprocessorSymbols,
            "stamp-3");
        engine.GetComponentContracts(
                referenceChangedContext,
                ScriptSemanticFixture.DocumentFilePath,
                liveSource,
                default)
            .ShouldNotBeNull();
        engine.ComponentContractBuildCount.ShouldBe(3);
    }

    [Fact]
    public void GetComponentContracts_LiveDocumentEdit_ReusesStableProjectClosure()
    {
        const string siblingSource =
            "<template><div /></template>\n" +
            "@script {\n" +
            "    [Parameter]\n" +
            "    public string SiblingValue { get; set; } = string.Empty;\n" +
            "}\n";
        const string liveAlphaSource =
            "<template><Button />{{ Alpha }}</template>\n" +
            "@script {\n" +
            "    [Parameter]\n" +
            "    public string Alpha { get; set; } = string.Empty;\n" +
            "}\n";
        var liveBetaSource = liveAlphaSource.Replace("Alpha", "Beta");
        var context = ScriptSemanticFixture.CreateContext(
            "stable-context",
            new LanguageProjectSourceDocument(
                "C:/workspace/App/Button.viu",
                siblingSource,
                IsComponent: true));
        var engine = new ScriptSemanticEngine();

        var alphaContracts = engine.GetComponentContracts(
            context,
            ScriptSemanticFixture.DocumentFilePath,
            liveAlphaSource,
            default);
        var betaContracts = engine.GetComponentContracts(
            context,
            ScriptSemanticFixture.DocumentFilePath,
            liveBetaSource,
            default);

        alphaContracts.ShouldNotBeNull();
        betaContracts.ShouldNotBeNull();
        alphaContracts!
            .Single(declaration => declaration.CompilerDeclaration.Name == "AppShell")
            .CompilerDeclaration.Parameters
            .ShouldContain(parameter => parameter.PropertyName == "Alpha");
        betaContracts!
            .Single(declaration => declaration.CompilerDeclaration.Name == "AppShell")
            .CompilerDeclaration.Parameters
            .ShouldContain(parameter => parameter.PropertyName == "Beta");
        betaContracts
            .Single(declaration => declaration.CompilerDeclaration.Name == "Button")
            .CompilerDeclaration.Parameters
            .ShouldContain(parameter => parameter.PropertyName == "SiblingValue");
        engine.ProjectStateBuildCount.ShouldBe(1);
        engine.ComponentContractBuildCount.ShouldBe(1);
        engine.ComponentProjectionBuildCount.ShouldBe(3);
    }

    [Fact]
    public void GetComponentContracts_SameStampReferenceReplacement_InvalidatesStableProjectClosure()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "viu-component-contract-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var referencePath = Path.Combine(temporaryDirectory, "Referenced.Components.dll");
            WriteComponentReference(referencePath, "Alpha");
            File.SetLastWriteTimeUtc(referencePath, DateTime.UtcNow.AddMinutes(-2));
            var referencePaths = ScriptSemanticFixture.ReferenceAssemblyPaths()
                .Concat([referencePath])
                .ToArray();
            var context = new LanguageProjectContext(
                ScriptSemanticFixture.ProjectFilePath,
                ScriptSemanticFixture.ProjectDirectory,
                ScriptSemanticFixture.RootNamespace,
                referencePaths,
                Array.Empty<LanguageProjectSourceDocument>(),
                ScriptSemanticFixture.PreprocessorSymbols,
                "unchanged-stamp");
            var engine = new ScriptSemanticEngine();
            const string liveSource = "<template><ReferencedWidget /></template>\n";

            var alphaContracts = engine.GetComponentContracts(
                context,
                ScriptSemanticFixture.DocumentFilePath,
                liveSource,
                default);

            WriteComponentReference(referencePath, "Beta");
            File.SetLastWriteTimeUtc(referencePath, DateTime.UtcNow.AddMinutes(2));
            var betaContracts = engine.GetComponentContracts(
                context,
                ScriptSemanticFixture.DocumentFilePath,
                liveSource,
                default);

            alphaContracts.ShouldNotBeNull();
            betaContracts.ShouldNotBeNull();
            alphaContracts!
                .Single(declaration => declaration.CompilerDeclaration.Name == "ReferencedWidget")
                .CompilerDeclaration.Parameters
                .ShouldContain(parameter => parameter.PropertyName == "Alpha");
            betaContracts!
                .Single(declaration => declaration.CompilerDeclaration.Name == "ReferencedWidget")
                .CompilerDeclaration.Parameters
                .ShouldContain(parameter => parameter.PropertyName == "Beta");
            engine.ProjectStateBuildCount.ShouldBe(2);
            engine.ComponentContractBuildCount.ShouldBe(2);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void CloseDocument_ComponentContractCache_EvictsDocumentProjectState()
    {
        var engine = new ScriptSemanticEngine();
        var context = ScriptSemanticFixture.CreateContext("close-eviction");
        const string liveSource = "<template><div /></template>\n";

        engine.GetComponentContracts(
                context,
                ScriptSemanticFixture.DocumentFilePath,
                liveSource,
                default)
            .ShouldNotBeNull();
        engine.ProjectStateBuildCount.ShouldBe(1);
        engine.ComponentContractBuildCount.ShouldBe(1);

        engine.CloseDocument(ScriptSemanticFixture.DocumentUri);
        using var staleRequestCancellation = new CancellationTokenSource();
        staleRequestCancellation.Cancel();

        Should.Throw<OperationCanceledException>(
            () => engine.GetComponentContracts(
                context,
                ScriptSemanticFixture.DocumentFilePath,
                liveSource,
                staleRequestCancellation.Token));
        engine.ProjectStateBuildCount.ShouldBe(1);
        engine.ComponentContractBuildCount.ShouldBe(1);

        engine.GetComponentContracts(
                context,
                ScriptSemanticFixture.DocumentFilePath,
                liveSource,
                default)
            .ShouldNotBeNull();

        engine.ProjectStateBuildCount.ShouldBe(2);
        engine.ComponentContractBuildCount.ShouldBe(2);
    }

    [Fact]
    public void GetHover_CanceledAfterClose_DoesNotPopulateSemanticCacheAndReopenBuildsFreshState()
    {
        const string siblingSource =
            "<template><button>{{ Label }}</button></template>\n" +
            "@script {\n" +
            "    [Parameter]\n" +
            "    public string Label { get; set; } = string.Empty;\n" +
            "}\n";
        const string liveSource = "<template><Button></Button></template>\n";
        var context = ScriptSemanticFixture.CreateContext(
            "close-race-cache",
            new LanguageProjectSourceDocument(
                "C:/workspace/App/Button.viu",
                siblingSource,
                IsComponent: true));
        var service = (ViuLanguageService)LanguageServices.Create();
        service.ConfigureProjectContext(ScriptSemanticFixture.DocumentUri, context);
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, liveSource, 1);
        var hoverPosition = TextCoordinateConverter.GetPosition(
            liveSource,
            liveSource.IndexOf("Button", StringComparison.Ordinal) + 1);

        service.CloseDocument(ScriptSemanticFixture.DocumentUri).ShouldBeTrue();
        using var staleRequestCancellation = new CancellationTokenSource();
        staleRequestCancellation.Cancel();

        Should.Throw<OperationCanceledException>(
            () => service.GetHover(
                ScriptSemanticFixture.DocumentUri,
                hoverPosition,
                staleRequestCancellation.Token));
        service.ProjectStateBuildCount.ShouldBe(0);
        service.ComponentContractBuildCount.ShouldBe(0);

        service.ConfigureProjectContext(ScriptSemanticFixture.DocumentUri, context);
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, liveSource, 2);

        service.GetHover(ScriptSemanticFixture.DocumentUri, hoverPosition).ShouldNotBeNull();
        service.ProjectStateBuildCount.ShouldBe(1);
        service.ComponentContractBuildCount.ShouldBe(1);
    }

    [Fact]
    public void CompletionThenHover_IdenticalDocumentAndContext_ShareComponentContractCache()
    {
        const string siblingSource =
            "<template><button>{{ Label }}</button></template>\n" +
            "@script {\n" +
            "    [Parameter]\n" +
            "    public string Label { get; set; } = string.Empty;\n" +
            "}\n";
        const string liveSource = "<template><Button :la></Button></template>\n";
        const string completionMarker = "<Button :la";
        var service = (ViuLanguageService)LanguageServices.Create();
        service.ConfigureProjectContext(
            ScriptSemanticFixture.DocumentUri,
            ScriptSemanticFixture.CreateContext(
                "shared-feature-cache",
                new LanguageProjectSourceDocument(
                    "C:/workspace/App/Button.viu",
                    siblingSource,
                    IsComponent: true)));
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, liveSource, 1);
        var completionOffset = liveSource.IndexOf(
            completionMarker,
            StringComparison.Ordinal) + completionMarker.Length;
        var hoverOffset = liveSource.IndexOf("Button", StringComparison.Ordinal) + 1;

        var completions = service.GetCompletions(
            ScriptSemanticFixture.DocumentUri,
            TextCoordinateConverter.GetPosition(liveSource, completionOffset));

        completions.ShouldContain(completion => completion.Label == ":label");
        service.ComponentContractBuildCount.ShouldBe(1);
        service.ComponentProjectionBuildCount.ShouldBe(2);

        var hover = service.GetHover(
            ScriptSemanticFixture.DocumentUri,
            TextCoordinateConverter.GetPosition(liveSource, hoverOffset));

        hover.ShouldNotBeNull();
        service.ComponentContractBuildCount.ShouldBe(1);
        service.ComponentProjectionBuildCount.ShouldBe(2);
    }

    private static void WriteComponentReference(string filePath, string propertyName)
    {
        var source =
            "using Assimalign.Viu.Components;\n" +
            "\n" +
            "public sealed class ReferencedWidget : IComponent\n" +
            "{\n" +
            "    [Parameter]\n" +
            $"    public string {propertyName} {{ get; set; }} = string.Empty;\n" +
            "\n" +
            "    public ComponentRenderer Setup(ComponentContext context)\n" +
            "        => throw new System.NotSupportedException();\n" +
            "}\n";
        var compilation = CSharpCompilation.Create(
            "Referenced.Components",
            [CSharpSyntaxTree.ParseText(source)],
            ScriptSemanticFixture.ReferenceAssemblyPaths()
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, emitResult.Diagnostics));
        }

        File.WriteAllBytes(filePath, stream.ToArray());
    }
}
