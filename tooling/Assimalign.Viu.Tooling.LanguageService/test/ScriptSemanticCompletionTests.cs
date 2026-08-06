using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// Pins the <see cref="ViuLanguageService.GetCompletions"/> wiring of the semantic engine
/// ([V01.01.12.23], #259): with an active project context the LookupSymbols items REPLACE the
/// declared-member list while the scaffold and keyword catalogs still append under the existing
/// dedup and SortText bands, and ANY engine miss — no context, an unreadable reference, an
/// unmapped position — falls through byte-identically to today's syntax-only path.
/// </summary>
public class ScriptSemanticCompletionTests
{
    private const string DocumentUri = ScriptSemanticFixture.DocumentUri;

    private const string ComponentSource =
        "<template>\n" +
        "  <div>x</div>\n" +
        "</template>\n" +
        "@script {\n" +
        "/// <summary>Counts clicks.</summary>\n" +
        "public int Count { get; set; }\n" +
        "    \n" +
        "}\n";

    [Fact]
    public void GetCompletions_ActiveProjectContext_ReplacesDeclaredListAndKeepsCatalogs()
    {
        var service = CreateService(ComponentSource, ScriptSemanticFixture.CreateContext());

        var completions = service.GetCompletions(
            DocumentUri,
            PositionAfter(ComponentSource, "set; }\n    "));

        var item = completions.Single(completion => completion.Label == "Count");
        // The semantic detail comes from the real compilation — the syntax-only declared reader
        // answers with the bare declared type ("int") instead.
        item.Detail.ShouldBe("int Count { get; set; }");
        item.SortText.ShouldBe("00:Count");
        completions.ShouldContain(completion => completion.Label == "OnSetup");
        completions.ShouldContain(completion => completion.Label == "using");
    }

    [Fact]
    public void GetCompletions_ActiveProjectContextTypedPrefix_FiltersSemanticAndKeywordItems()
    {
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "public int Count { get; set; }\n" +
            "    co\n" +
            "}\n";
        var service = CreateService(source, ScriptSemanticFixture.CreateContext());

        var completions = service.GetCompletions(DocumentUri, PositionAfter(source, "\n    co"));

        completions.ShouldContain(completion => completion.Label == "Count");
        completions.ShouldContain(completion => completion.Label == "const");
        completions.ShouldAllBe(
            completion => completion.Label.StartsWith(
                "co",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetCompletions_ContextMemberAccessWithActiveContext_BindsThroughRealCompilation()
    {
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "public void Handle()\n" +
            "{\n" +
            "    Context.\n" +
            "}\n" +
            "}\n";
        var service = CreateService(source, ScriptSemanticFixture.CreateContext());

        var completions = service.GetCompletions(
            DocumentUri,
            PositionAfter(source, "    Context."));

        // The syntax-only path answers with the static catalog instance; the semantic path binds
        // the generated Context property's IComponentContext through the real compilation.
        completions.ShouldNotBeSameAs(ViuCompletionCatalog.ContextMembers);
        completions.Single(completion => completion.Label == "Lifecycle")
            .SortText.ShouldBe("00:Lifecycle");
        // The appended Context. scaffold catalog loses the first-wins dedup to the bound member.
        completions.Single(completion => completion.Label == "Arguments")
            .SortText.ShouldBe("00:Arguments");
        // Keywords never follow a dot.
        completions.ShouldNotContain(completion => completion.Label == "using");
    }

    [Fact]
    public void GetCompletions_EditorBrowsableNeverRenderHelperMembers_OmitsCompilerSeam()
    {
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "public int Count { get; set; }\n" +
            "    \n" +
            "}\n";
        var renderHelpers = new LanguageProjectSourceDocument(
            "C:\\workspace\\App\\RenderHelpers.cs",
            "namespace Assimalign.Viu;\n" +
            "\n" +
            "public static class RenderHelpers\n" +
            "{\n" +
            "    [System.ComponentModel.EditorBrowsable(" +
            "System.ComponentModel.EditorBrowsableState.Never)]\n" +
            "    public static readonly object _Fragment = new();\n" +
            "\n" +
            "    [System.ComponentModel.EditorBrowsable(" +
            "System.ComponentModel.EditorBrowsableState.Never)]\n" +
            "    public static object _createVNode() => new();\n" +
            "\n" +
            "    public static object CompletionControl => new();\n" +
            "}\n",
            IsComponent: false);
        var service = CreateService(
            source,
            ScriptSemanticFixture.CreateContext(renderHelpers));

        var completions = service.GetCompletions(
            DocumentUri,
            PositionAfter(source, "set; }\n    "));

        // [V01.01.14.02] RenderHelpers is statically imported into the generated unit containing
        // @script, so filtering must honor EditorBrowsable on each imported member.
        completions.ShouldContain(completion => completion.Label == "CompletionControl");
        completions.ShouldNotContain(
            completion => completion.Label.StartsWith("_", StringComparison.Ordinal));
    }

    [Fact]
    public void GetCompletions_WithoutProjectContext_IsByteIdenticalToSyntaxOnlyPath()
    {
        var position = PositionAfter(ComponentSource, "set; }\n    ");
        var syntaxOnly = CreateService(ComponentSource, context: null);
        // A context for a DIFFERENT document must not activate semantics for this one.
        var configuredElsewhere = CreateService(
            ComponentSource,
            ScriptSemanticFixture.CreateContext(),
            contextDocumentUri: "file:///C:/workspace/App/Other.viu");

        var expected = syntaxOnly.GetCompletions(DocumentUri, position);
        var actual = configuredElsewhere.GetCompletions(DocumentUri, position);

        actual.ShouldBe(expected);
        expected.ShouldContain(
            completion => completion.Label == "Count" && completion.Detail == "int");
    }

    [Fact]
    public void GetCompletions_UnreadableReferenceEngineMiss_FallsBackToSyntaxOnlyPath()
    {
        var position = PositionAfter(ComponentSource, "set; }\n    ");
        var missContext = new LanguageProjectContext(
            ScriptSemanticFixture.ProjectFilePath,
            ScriptSemanticFixture.ProjectDirectory,
            ScriptSemanticFixture.RootNamespace,
            ["C:\\does\\not\\exist\\missing.dll"],
            [],
            ScriptSemanticFixture.PreprocessorSymbols,
            "stamp-miss");
        var syntaxOnly = CreateService(ComponentSource, context: null);
        var missing = CreateService(ComponentSource, missContext);

        var expected = syntaxOnly.GetCompletions(DocumentUri, position);
        var actual = missing.GetCompletions(DocumentUri, position);

        actual.ShouldBe(expected);
    }

    [Fact]
    public void GetCompletions_UnmappedPositionEngineMiss_FallsBackToSyntaxOnlyPath()
    {
        // An empty script block emits no #line region, so even with an active context the
        // position never maps and the syntax-only answer is served unchanged.
        const string source = "@script {}\n";
        var position = PositionAfter(source, "@script {");
        var syntaxOnly = CreateService(source, context: null);
        var configured = CreateService(source, ScriptSemanticFixture.CreateContext());

        var expected = syntaxOnly.GetCompletions(DocumentUri, position);
        var actual = configured.GetCompletions(DocumentUri, position);

        actual.ShouldBe(expected);
        expected.ShouldNotBeEmpty();
    }

    [Fact]
    public void ResolveCompletionDocumentation_SemanticLabel_ReturnsSymbolDocumentation()
    {
        var service = CreateService(ComponentSource, ScriptSemanticFixture.CreateContext());
        service.GetCompletions(DocumentUri, PositionAfter(ComponentSource, "set; }\n    "));

        var documentation = service.ResolveCompletionDocumentation(DocumentUri, "Count");

        documentation.ShouldNotBeNull();
        documentation.ShouldContain("int Count { get; set; }");
        documentation.ShouldContain("Counts clicks.");
    }

    [Fact]
    public void CloseDocument_AfterSemanticCompletion_ClearsResolutionState()
    {
        var service = CreateService(ComponentSource, ScriptSemanticFixture.CreateContext());
        service.GetCompletions(DocumentUri, PositionAfter(ComponentSource, "set; }\n    "));

        service.CloseDocument(DocumentUri).ShouldBeTrue();

        service.ResolveCompletionDocumentation(DocumentUri, "Count").ShouldBeNull();
    }

    private static IViuLanguageService CreateService(
        string source,
        LanguageProjectContext? context,
        string? contextDocumentUri = null)
    {
        var service = ViuLanguageServices.Create();
        if (context is not null)
        {
            ((IScriptSemanticLanguageService)service).ConfigureProjectContext(
                contextDocumentUri ?? DocumentUri,
                context);
        }

        service.OpenDocument(DocumentUri, source, 1);
        return service;
    }

    private static LanguagePosition PositionAfter(string source, string marker)
    {
        var offset = source.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return TextCoordinateConverter.GetPosition(source, offset);
    }
}
