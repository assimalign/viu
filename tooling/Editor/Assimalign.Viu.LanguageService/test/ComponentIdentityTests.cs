using System;
using System.Linq;
using System.Threading;

using Shouldly;
using Xunit;

using Assimalign.Viu.Compiler.Css;
using Assimalign.Viu.Compiler.SingleFileComponent;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins build/editor component identity agreement, including changes to the emitted path set,
/// under [V01.01.06.16], [SFC-CG-5], [SFC-CG-10], and [VUE-7].
/// </summary>
public class ComponentIdentityTests
{
    private const string LayoutPath = "C:/workspace/App/Pages/Blog.viu";
    private const string EntryPath = "C:/workspace/App/Pages/Blog/Entry.viu";
    private const string LayoutSource = "<template><div /></template>\n";

    [Fact]
    public void Project_SiblingDirectory_MovesOnlyLayoutAndPreservesRegistrationAndHintName()
    {
        var context = ScriptSemanticFixture.CreateContext() with
        {
            ComponentFilePaths = [LayoutPath, EntryPath],
        };

        var layout = Project(LayoutPath, context);
        var entry = Project(EntryPath, context);

        layout.Model.Namespace.ShouldBe("Test.App.Pages.GeneratedComponents");
        layout.Model.ClassName.ShouldBe("Blog");
        layout.Model.HintName.ShouldBe("Pages.Blog.SingleFileComponent.g.cs");
        entry.Model.Namespace.ShouldBe("Test.App.Pages.Blog");
        entry.Model.HintName.ShouldBe("Pages.Blog.Entry.SingleFileComponent.g.cs");
        SingleFileComponentSourceEmitter.Emit(layout.Model)
            .ShouldContain("ComponentReference.ForName(\"Blog\")");
        layout.Diagnostics.ShouldBeEmpty();
        entry.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void CreateInput_ShadowedVuePeer_DoesNotDiscriminateCanonicalHintName()
    {
        var context = ScriptSemanticFixture.CreateContext() with
        {
            ComponentFilePaths = [LayoutPath, LayoutPath.Replace(".viu", ".vue"), EntryPath],
        };

        var canonical = LanguageDocumentProjection.CreateInput(
            SingleFileComponentFormat.Viu, LayoutPath, LayoutSource, context);
        var shadowed = LanguageDocumentProjection.CreateInput(
            SingleFileComponentFormat.Vue, LayoutPath.Replace(".viu", ".vue"), LayoutSource, context);

        canonical.HintName.ShouldBe("Pages.Blog.SingleFileComponent.g.cs");
        canonical.HasIdentityCollision.ShouldBeFalse();
        canonical.HasCanonicalPeer.ShouldBeFalse();
        shadowed.HasCanonicalPeer.ShouldBeTrue();
    }

    [Fact]
    public void CreateInput_CaseDifferentViuPaths_DiscriminatesBothHintsOnEveryHost()
    {
        const string upperPath = "C:/workspace/App/Pages/Card.viu";
        const string lowerPath = "C:/workspace/App/Pages/card.viu";
        var paths = new[] { upperPath, lowerPath };
        var context = ScriptSemanticFixture.CreateContext() with { ComponentFilePaths = paths };
        SingleFileComponentNameResolver.SelectCaseCollidingPaths(paths, context.ProjectDirectory)
            .ShouldBe(paths);

        foreach (var path in paths)
        {
            var input = LanguageDocumentProjection.CreateInput(
                SingleFileComponentFormat.Viu, path, LayoutSource, context);
            var expected = SingleFileComponentNameResolver.Resolve(path,
                context.ProjectDirectory, context.RootNamespace, requiresCaseDiscriminator: true);
            input.HintName.ShouldBe(expected.HintName);
            input.Namespace.ShouldBe(expected.Namespace);
            input.HasIdentityCollision.ShouldBeFalse();
        }
    }

    [Fact]
    public void CreateInput_LiveWindowsPathAlias_ReusesDeclaredIdentityWithoutFalseCollision()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string aliasPath = "c:/WORKSPACE/App/Pages/Blog.viu";
        var context = ScriptSemanticFixture.CreateContext() with { ComponentFilePaths = [LayoutPath] };
        var ordinary = LanguageDocumentProjection.CreateInput(
            SingleFileComponentFormat.Viu, aliasPath, LayoutSource, context);

        ordinary.Namespace.ShouldBe("Test.App.Pages");
        ordinary.HintName.ShouldBe("Pages.Blog.SingleFileComponent.g.cs");
        ordinary.HasIdentityCollision.ShouldBeFalse();
        ordinary.FilePath.ShouldBe(aliasPath);
        ordinary.CssHashSalt.ShouldBe(CssComponentHash.Resolve(LayoutPath, context.ProjectDirectory));

        var expandedContext = context with { ComponentFilePaths = [LayoutPath, EntryPath] };
        var relocated = LanguageDocumentProjection.CreateInput(
            SingleFileComponentFormat.Viu, aliasPath, LayoutSource, expandedContext);
        relocated.Namespace.ShouldBe("Test.App.Pages.GeneratedComponents");
        relocated.HintName.ShouldBe(ordinary.HintName);
        relocated.HasIdentityCollision.ShouldBeFalse();
        LanguageDocumentProjection.GetComponentPaths(aliasPath, expandedContext).Length.ShouldBe(2);
    }

    [Fact]
    public void GetDiagnostics_ResidualCollision_ReportsLocatedIdentityError()
    {
        var context = ScriptSemanticFixture.CreateContext() with
        {
            ComponentFilePaths = [LayoutPath, EntryPath, "C:/workspace/App/Pages/GeneratedComponents/Blog.viu"],
        };
        var documentUri = new Uri(LayoutPath).AbsoluteUri;
        var service = LanguageServices.Create();
        ((IScriptSemanticLanguageService)service).ConfigureProjectContext(documentUri, context);
        service.OpenDocument(documentUri, LayoutSource, 1);

        var diagnostics = service.GetDiagnostics(documentUri);

        var diagnostic = diagnostics.Single(item => item.Code == "VIU1005");
        diagnostic.Severity.ShouldBe(LanguageDiagnosticSeverity.Error);
        diagnostic.Range.Start.ShouldBe(new LanguagePosition(0, 0));
        diagnostics.ShouldNotContain(item => item.Code == "CS0101");
    }

    [Fact]
    public void GetComponentContracts_ResidualCollision_ExcludesSuppressedLiveRegistration()
    {
        var context = ScriptSemanticFixture.CreateContext() with
        {
            ComponentFilePaths = [LayoutPath, EntryPath, "C:/workspace/App/Pages/GeneratedComponents/Blog.viu"],
        };
        var engine = new ScriptSemanticEngine();

        var contracts = engine.GetComponentContracts(context, LayoutPath, LayoutSource, CancellationToken.None);

        contracts.ShouldNotBeNull();
        contracts.ShouldNotContain(item => item.CompilerDeclaration.Name == "Blog");
    }

    [Fact]
    public void GetCompletions_LiveNestedComponent_IncludesItsPathWhenProjectingSiblingLayout()
    {
        var engine = new ScriptSemanticEngine();
        var context = ScriptSemanticFixture.CreateContext(
            new LanguageProjectSourceDocument(LayoutPath, LayoutSource, IsComponent: true));

        var result = CompleteNamespace(engine, context, EntryPath, "Test.App.Pages.GeneratedComponents.");

        result.Items.ShouldContain(item => item.Label == "Blog" && item.Kind == LanguageCompletionItemKind.Class);
    }

    [Fact]
    public void GetCompletions_ComponentPathsChange_ReprojectsUnchangedSiblingAndReusesStableSet()
    {
        var engine = new ScriptSemanticEngine();
        var initialContext = ScriptSemanticFixture.CreateContext(
            new LanguageProjectSourceDocument(LayoutPath, LayoutSource, IsComponent: true));
        var initial = CompleteNamespace(engine, initialContext, ScriptSemanticFixture.DocumentFilePath, "Test.App.Pages.");
        initial.Items.ShouldContain(item => item.Label == "Blog" && item.Kind == LanguageCompletionItemKind.Class);
        engine.ProjectStateBuildCount.ShouldBe(1);
        engine.SourceTreeBuildCount.ShouldBe(1);

        // The host's stamp and sibling text stay unchanged; the complete path set alone invalidates
        // the sibling identity. [SFC-CG-10]
        var expandedContext = initialContext with { ComponentFilePaths = [LayoutPath, EntryPath] };
        var changed = CompleteNamespace(engine, expandedContext, ScriptSemanticFixture.DocumentFilePath, "Test.App.Pages.");
        changed.Items.ShouldContain(item => item.Label == "GeneratedComponents" && item.Kind == LanguageCompletionItemKind.Module);
        changed.Items.ShouldNotContain(item => item.Label == "Blog" && item.Kind == LanguageCompletionItemKind.Class);
        engine.ProjectStateBuildCount.ShouldBe(2);
        engine.SourceTreeBuildCount.ShouldBe(2);

        CompleteNamespace(engine, expandedContext, ScriptSemanticFixture.DocumentFilePath, "Test.App.Pages.");
        engine.ProjectStateBuildCount.ShouldBe(2);
        engine.SourceTreeBuildCount.ShouldBe(2);

        var restored = CompleteNamespace(engine, initialContext, ScriptSemanticFixture.DocumentFilePath, "Test.App.Pages.");
        restored.Items.ShouldContain(item => item.Label == "Blog" && item.Kind == LanguageCompletionItemKind.Class);
        engine.ProjectStateBuildCount.ShouldBe(3);
        engine.SourceTreeBuildCount.ShouldBe(3);
    }

    [Fact]
    public void GetCompletions_ShadowedVuePeer_DoesNotContributeComponentMembers()
    {
        var engine = new ScriptSemanticEngine();
        var context = ScriptSemanticFixture.CreateContext(
            new LanguageProjectSourceDocument(LayoutPath, "@script {\n    public int Canonical;\n}\n", IsComponent: true),
            new LanguageProjectSourceDocument(LayoutPath.Replace(".viu", ".vue"),
                "<script lang=\"csharp\">public int Shadowed;</script>\n", IsComponent: true));
        const string source = "@script {\n    public Test.App.Pages.Blog Layout = new();\n    public void Handle()\n    {\n        Layout.\n    }\n}\n";

        var result = engine.GetCompletions(context, ScriptSemanticFixture.DocumentUri,
            ScriptSemanticFixture.DocumentFilePath, source,
            source.IndexOf("Layout.\n", StringComparison.Ordinal) + "Layout.".Length,
            string.Empty, ScriptCompletionContextKind.Expression, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Items.ShouldContain(item => item.Label == "Canonical");
        result.Items.ShouldNotContain(item => item.Label == "Shadowed");
    }

    private static SingleFileComponentProjectionResult Project(string filePath, LanguageProjectContext context)
        => LanguageDocumentProjection.Project(
            LanguageDocument.Create(new Uri(filePath).AbsoluteUri, LayoutSource, 1), context,
            CancellationToken.None);

    private static ScriptSemanticCompletionResult CompleteNamespace(
        ScriptSemanticEngine engine, LanguageProjectContext context, string filePath, string namespacePrefix)
    {
        var source = "@script {\n    " + namespacePrefix + "\n}\n";
        var result = engine.GetCompletions(context, new Uri(filePath).AbsoluteUri, filePath, source,
            source.IndexOf(namespacePrefix, StringComparison.Ordinal) + namespacePrefix.Length,
            string.Empty, ScriptCompletionContextKind.Expression, CancellationToken.None);
        result.ShouldNotBeNull();
        return result;
    }
}
