using System;
using System.Linq;
using System.Threading;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins the SemanticModel-only completion engine ([V01.01.12.23], #259) over in-memory
/// <see cref="LanguageProjectContext"/> fixtures: declared members answer with type details from a
/// REAL compilation, sibling <c>.cs</c> partials and sibling components participate, members typed
/// from metadata references bind through the real binder, and the base-compilation cache reuses or
/// per-tree-swaps rather than rebuilding.
/// </summary>
public class ScriptSemanticEngineTests
{
    private const string DocumentUri = ScriptSemanticFixture.DocumentUri;
    private const string DocumentFilePath = ScriptSemanticFixture.DocumentFilePath;

    private const string ComponentSource =
        "<template>\n" +
        "  <div>x</div>\n" +
        "</template>\n" +
        "@script {\n" +
        "public int Count { get; set; }\n" +
        "    \n" +
        "}\n";

    [Fact]
    public void GetCompletions_DeclaredProperty_OffersTypeDetailFromCompilation()
    {
        var engine = new ScriptSemanticEngine();

        var result = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext(),
            DocumentUri,
            DocumentFilePath,
            ComponentSource,
            OffsetAfter(ComponentSource, "set; }\n    "),
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);

        result.ShouldNotBeNull();
        result.IsMemberAccess.ShouldBeFalse();
        var item = result.Items.Single(item => item.Label == "Count");
        item.Kind.ShouldBe(LanguageCompletionItemKind.Property);
        item.Detail.ShouldBe("int Count { get; set; }");
        item.SortText.ShouldBe("00:Count");
    }

    [Fact]
    public void GetCompletions_SiblingPartialClassDocument_OffersItsMember()
    {
        var engine = new ScriptSemanticEngine();
        var sibling = new LanguageProjectSourceDocument(
            "C:\\workspace\\App\\AppShell.Partial.cs",
            "namespace Test.App;\n" +
            "\n" +
            "partial class AppShell\n" +
            "{\n" +
            "    public int SiblingValue => 42;\n" +
            "}\n",
            IsComponent: false);

        var result = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext(sibling),
            DocumentUri,
            DocumentFilePath,
            ComponentSource,
            OffsetAfter(ComponentSource, "set; }\n    "),
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);

        result.ShouldNotBeNull();
        var item = result.Items.Single(item => item.Label == "SiblingValue");
        item.Kind.ShouldBe(LanguageCompletionItemKind.Property);
        item.Detail.ShouldBe("int SiblingValue { get; }");
    }

    [Fact]
    public void GetCompletions_SiblingComponentDocument_OffersItsGeneratedClass()
    {
        var engine = new ScriptSemanticEngine();
        var widget = new LanguageProjectSourceDocument(
            "C:\\workspace\\App\\Widget.viu",
            "@script {\npublic int WidgetValue;\n}\n",
            IsComponent: true);

        var result = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext(widget),
            DocumentUri,
            DocumentFilePath,
            ComponentSource,
            OffsetAfter(ComponentSource, "set; }\n    "),
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);

        result.ShouldNotBeNull();
        result.Items.ShouldContain(
            item => item.Label == "Widget" && item.Kind == LanguageCompletionItemKind.Class);
    }

    [Fact]
    public void GetCompletions_MetadataTypedMemberAccess_ListsMetadataMembers()
    {
        // `Title` is `string`, so every offered member comes from a METADATA reference — the
        // running machine's own framework assemblies fed through the context.
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "public string Title { get; } = \"Viu\";\n" +
            "public void Handle()\n" +
            "{\n" +
            "    Title.\n" +
            "}\n" +
            "}\n";
        var engine = new ScriptSemanticEngine();

        var result = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext(),
            DocumentUri,
            DocumentFilePath,
            source,
            OffsetAfter(source, "    Title."),
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);

        result.ShouldNotBeNull();
        result.IsMemberAccess.ShouldBeTrue();
        result.MemberAccessTargetText.ShouldBe("Title");
        var item = result.Items.Single(item => item.Label == "Length");
        item.Kind.ShouldBe(LanguageCompletionItemKind.Property);
        item.Detail.ShouldBe("int Length { get; }");
        // Static members are not reachable through an instance receiver.
        result.Items.ShouldNotContain(item => item.Label == "Empty");
    }

    [Fact]
    public void GetCompletions_ClassLevelValueReceiverMemberAccess_ListsInstanceMembers()
    {
        // Typing `Title.` directly at member-declaration level (the packed-consumer probe A
        // shape): the incomplete member parses as a qualified TYPE name, and the type-only binder
        // reports neither a symbol nor a candidate for the property, so the engine re-resolves
        // the receiver by name at the position and still lists the instance members.
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "public string Title { get; } = \"Viu\";\n" +
            "\n" +
            "    Title.\n" +
            "}\n";
        var engine = new ScriptSemanticEngine();

        var result = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext(),
            DocumentUri,
            DocumentFilePath,
            source,
            OffsetAfter(source, "    Title."),
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);

        result.ShouldNotBeNull();
        result.IsMemberAccess.ShouldBeTrue();
        result.MemberAccessTargetText.ShouldBe("Title");
        result.Items.ShouldContain(
            item => item.Label == "Length" && item.Kind == LanguageCompletionItemKind.Property);
        // Static members are not reachable through an instance receiver.
        result.Items.ShouldNotContain(item => item.Label == "Empty");
    }

    [Fact]
    public void GetCompletions_ContextMemberAccess_BindsThroughRealBinder()
    {
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "public void Handle()\n" +
            "{\n" +
            "    Context.\n" +
            "}\n" +
            "}\n";
        var engine = new ScriptSemanticEngine();

        var result = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext(),
            DocumentUri,
            DocumentFilePath,
            source,
            OffsetAfter(source, "    Context."),
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);

        result.ShouldNotBeNull();
        result.IsMemberAccess.ShouldBeTrue();
        result.MemberAccessTargetText.ShouldBe("Context");
        result.Items.ShouldContain(
            item => item.Label == "Lifecycle" &&
                    item.Kind == LanguageCompletionItemKind.Property);
        result.Items.ShouldContain(
            item => item.Label == "Emit" && item.Kind == LanguageCompletionItemKind.Method);
    }

    [Fact]
    public void GetCompletions_AdoptedComponentBaseSource_UsesProjectContractWithoutCompatibilityBridge()
    {
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "public void Handle()\n" +
            "{\n" +
            "    Context.\n" +
            "}\n" +
            "}\n";
        var adoptedContract = new LanguageProjectSourceDocument(
            "C:\\workspace\\App\\ComponentModel.cs",
            "namespace Assimalign.Viu.Components;\n" +
            "\n" +
            "public abstract class ComponentBase\n" +
            "{\n" +
            "    protected ComponentContext? Context { get; set; }\n" +
            "}\n" +
            "\n" +
            "public abstract class ComponentContext\n" +
            "{\n" +
            "    public abstract object AdoptedControl { get; }\n" +
            "}\n",
            IsComponent: false);
        var engine = new ScriptSemanticEngine();

        var result = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext(adoptedContract),
            DocumentUri,
            DocumentFilePath,
            source,
            OffsetAfter(source, "    Context."),
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);

        result.ShouldNotBeNull();
        result.Items.ShouldContain(
            item => item.Label == "AdoptedControl"
                && item.Kind == LanguageCompletionItemKind.Property);
        result.Items.ShouldNotContain(item => item.Label == "Arguments");
    }

    [Fact]
    public void GetCompletions_NamespaceQualifierAtClassLevel_ListsNamespaceTypes()
    {
        // An incomplete member declaration at class level parses as a qualified name, exercising
        // the type-position branch of the member-access classification.
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "    System.Str\n" +
            "}\n";
        var engine = new ScriptSemanticEngine();

        var result = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext(),
            DocumentUri,
            DocumentFilePath,
            source,
            OffsetAfter(source, "System.Str"),
            "Str",
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);

        result.ShouldNotBeNull();
        result.IsMemberAccess.ShouldBeTrue();
        result.MemberAccessTargetText.ShouldBe("System");
        result.Items.ShouldContain(
            item => item.Label == "String" && item.Kind == LanguageCompletionItemKind.Class);
    }

    [Fact]
    public void GetCompletions_EqualCacheStampThenSingleFileChange_ReusesAndSwapsPerTree()
    {
        // Run-count pinning per .claude/rules/testing.md: an equal stamp reuses the base
        // compilation untouched, and a single changed sibling swaps exactly one tree via
        // ReplaceSyntaxTree instead of rebuilding the cone.
        var engine = new ScriptSemanticEngine();
        var siblingAlpha = new LanguageProjectSourceDocument(
            "C:\\workspace\\App\\Alpha.cs",
            "namespace Test.App;\n\npartial class AppShell\n{\n    public int Alpha => 1;\n}\n",
            IsComponent: false);
        var siblingBeta = new LanguageProjectSourceDocument(
            "C:\\workspace\\App\\Beta.cs",
            "namespace Test.App;\n\npartial class AppShell\n{\n    public int Beta => 2;\n}\n",
            IsComponent: false);
        var offset = OffsetAfter(ComponentSource, "set; }\n    ");

        var first = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext("stamp-1", siblingAlpha, siblingBeta),
            DocumentUri,
            DocumentFilePath,
            ComponentSource,
            offset,
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);
        first.ShouldNotBeNull();
        first.Items.ShouldContain(item => item.Label == "Alpha");
        first.Items.ShouldContain(item => item.Label == "Beta");
        engine.ProjectStateBuildCount.ShouldBe(1);
        engine.SourceTreeBuildCount.ShouldBe(2);

        var second = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext("stamp-1", siblingAlpha, siblingBeta),
            DocumentUri,
            DocumentFilePath,
            ComponentSource,
            offset,
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);
        second.ShouldNotBeNull();
        engine.ProjectStateBuildCount.ShouldBe(1);
        engine.SourceTreeBuildCount.ShouldBe(2);

        var changedBeta = siblingBeta with
        {
            Text = "namespace Test.App;\n\npartial class AppShell\n{\n    public int Gamma => 3;\n}\n",
        };
        var third = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext("stamp-2", siblingAlpha, changedBeta),
            DocumentUri,
            DocumentFilePath,
            ComponentSource,
            offset,
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);
        third.ShouldNotBeNull();
        third.Items.ShouldContain(item => item.Label == "Gamma");
        third.Items.ShouldNotContain(item => item.Label == "Beta");
        engine.ProjectStateBuildCount.ShouldBe(1);
        engine.SourceTreeBuildCount.ShouldBe(3);
    }

    [Fact]
    public void GetCompletions_UnreadableReferencePath_ReturnsNullMiss()
    {
        var engine = new ScriptSemanticEngine();
        var context = new LanguageProjectContext(
            ScriptSemanticFixture.ProjectFilePath,
            ScriptSemanticFixture.ProjectDirectory,
            ScriptSemanticFixture.RootNamespace,
            ["C:\\does\\not\\exist\\missing.dll"],
            [],
            ScriptSemanticFixture.PreprocessorSymbols,
            "stamp-miss");

        var result = engine.GetCompletions(
            context,
            DocumentUri,
            DocumentFilePath,
            ComponentSource,
            OffsetAfter(ComponentSource, "set; }\n    "),
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetCompletions_CanceledToken_ThrowsOperationCanceled()
    {
        var engine = new ScriptSemanticEngine();

        Should.Throw<OperationCanceledException>(
            () => engine.GetCompletions(
                ScriptSemanticFixture.CreateContext(),
                DocumentUri,
                DocumentFilePath,
                ComponentSource,
                OffsetAfter(ComponentSource, "set; }\n    "),
                string.Empty,
                ScriptCompletionContextKind.Expression,
                new CancellationToken(canceled: true)));
    }

    [Fact]
    public void ResolveDocumentation_CompletedLabel_ReturnsSignatureAndSummary()
    {
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "/// <summary>Counts clicks.</summary>\n" +
            "public int Count { get; set; }\n" +
            "    \n" +
            "}\n";
        var engine = new ScriptSemanticEngine();
        var result = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext(),
            DocumentUri,
            DocumentFilePath,
            source,
            OffsetAfter(source, "set; }\n    "),
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);
        result.ShouldNotBeNull();

        var documentation = engine.ResolveDocumentation(
            DocumentUri,
            "Count",
            CancellationToken.None);

        documentation.ShouldNotBeNull();
        documentation.ShouldContain("int Count { get; set; }");
        documentation.ShouldContain("Counts clicks.");
        engine.ResolveDocumentation(DocumentUri, "NotALabel", CancellationToken.None)
            .ShouldBeNull();
    }

    private static int OffsetAfter(string source, string marker)
        => source.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
}
