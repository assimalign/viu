using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// Pins the syntactic gating of the <c>@script</c> completion sources
/// ([V01.01.12.07.04], [V01.01.12.23]). The reported defect: a line reading <c>using </c> answered
/// with the component's own members — a private field included — plus the Viu scaffold catalog and
/// its snippets, none of which the C# grammar admits inside a using directive. Only namespaces and
/// types are legal there, so only namespaces and types are offered; every other position keeps the
/// full source list, because inside the component's own class body its private members genuinely
/// are in scope.
/// </summary>
public class ScriptUsingDirectiveCompletionTests
{
    private const string DocumentUri = ScriptSemanticFixture.DocumentUri;

    private const string ComponentSource =
        "<template>\n" +
        "  <div>x</div>\n" +
        "</template>\n" +
        "@script {\n" +
        "using System;\n" +
        "using \n" +
        "private int _beforeUpdateCount;\n" +
        "public int Count { get; set; }\n" +
        "}\n";

    [Fact]
    public void GetCompletions_UsingDirectiveWithActiveContext_OffersNamespacesAndTypesOnly()
    {
        var service = CreateService(ComponentSource, ScriptSemanticFixture.CreateContext());

        var completions = service.GetCompletions(DocumentUri, UsingDirectivePosition());

        completions.ShouldContain(completion => completion.Label == "System");
        // The defect in its exact reported shape: the component's own private field, its public
        // members, the Viu scaffold catalog, and the snippets are all illegal here.
        completions.ShouldNotContain(completion => completion.Label == "_beforeUpdateCount");
        completions.ShouldNotContain(completion => completion.Label == "Count");
        completions.ShouldNotContain(completion => completion.Label == "Context");
        completions.ShouldNotContain(completion => completion.Label == "Reactive");
        completions.ShouldNotContain(completion => completion.Label == "OnSetup");
        completions.ShouldNotContain(completion => completion.Label == "mounted callback");
        completions.ShouldNotContain(completion => completion.Label == "reactive reference");
        completions.ShouldNotContain(completion => completion.Label == "computed value");
        // Keywords are a lexical aid for member positions; a using directive's name is not one.
        completions.ShouldNotContain(
            completion => completion.Kind == LanguageCompletionItemKind.Keyword);
        completions.ShouldAllBe(completion => !completion.IsSnippet);
        completions.ShouldAllBe(
            completion => completion.Kind == LanguageCompletionItemKind.Module ||
                completion.Kind == LanguageCompletionItemKind.Class);
    }

    [Fact]
    public void GetCompletions_NamespaceSymbol_CarriesTheModuleKind()
    {
        // The Language Server Protocol has no namespace kind; Module is the value editors render
        // with the namespace glyph ({} in Visual Studio). Falling through to Text put namespaces
        // behind the plain-text glyph instead.
        var service = CreateService(ComponentSource, ScriptSemanticFixture.CreateContext());

        var completions = service.GetCompletions(DocumentUri, UsingDirectivePosition());

        completions.Single(completion => completion.Label == "System")
            .Kind.ShouldBe(LanguageCompletionItemKind.Module);
        ((int)LanguageCompletionItemKind.Module).ShouldBe(9);
    }

    [Fact]
    public void GetCompletions_UsingDirectiveWithActiveContext_SortsNamespacesAboveTypes()
    {
        var service = CreateService(ComponentSource, ScriptSemanticFixture.CreateContext());

        var completions = service.GetCompletions(DocumentUri, UsingDirectivePosition());

        // A using directive names a namespace far more often than a type (`using static` and the
        // alias form are the exceptions), so its two-band sort key puts every namespace above every
        // type regardless of alphabetical order.
        completions.Single(completion => completion.Label == "System")
            .SortText.ShouldBe("00:System");
        completions.ShouldAllBe(
            completion => completion.SortText.StartsWith("00:", StringComparison.Ordinal) ||
                completion.SortText.StartsWith("01:", StringComparison.Ordinal));
    }

    [Fact]
    public void GetCompletions_UsingDirectiveWithoutProjectContext_OffersNothing()
    {
        // The degraded answer is honest emptiness: without a compilation the service knows no
        // namespace, and the syntax-only member-and-snippet list is illegal in a using directive.
        var service = CreateService(ComponentSource, context: null);

        var completions = service.GetCompletions(DocumentUri, UsingDirectivePosition());

        completions.ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_PartiallyTypedUsingKeyword_IsStillAMemberPosition()
    {
        // `usin` is a partially typed keyword, not a directive that has reached its name: the
        // keyword catalog must still answer it.
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "usin\n" +
            "}\n";
        var service = CreateService(source, context: null);

        var completions = service.GetCompletions(DocumentUri, PositionAfter(source, "usin"));

        completions.ShouldContain(completion => completion.Label == "using");
    }

    [Fact]
    public void GetCompletions_CompletedUsingDirective_IsStillAMemberPosition()
    {
        // The semicolon closes the directive; what follows it on the line is an ordinary position.
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "using System; \n" +
            "}\n";
        var service = CreateService(source, context: null);

        var completions = service.GetCompletions(DocumentUri, PositionAfter(source, "using System; "));

        completions.ShouldContain(completion => completion.Label == "Context");
    }

    [Fact]
    public void GetCompletions_UsingStatementInsideMethodBody_IsAnExpressionPosition()
    {
        // A `using` inside a method body is a statement, not a directive: the brace depth is what
        // separates the two, and members remain legal there.
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "public void Handle()\n" +
            "{\n" +
            "    using \n" +
            "}\n" +
            "}\n";
        var service = CreateService(source, context: null);

        var completions = service.GetCompletions(DocumentUri, PositionAfter(source, "    using "));

        completions.ShouldContain(completion => completion.Label == "Context");
        completions.ShouldContain(completion => completion.Label == "var");
    }

    [Fact]
    public void GetCompletions_MethodBodyWithActiveContext_StillOffersOwnPrivateMembers()
    {
        // The nuance this gate must NOT break: inside the component's own class body a private
        // member is genuinely accessible, and SemanticModel.LookupSymbols honors exactly that
        // accessibility. Suppressing privates globally would be a regression, not a fix.
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "private int _beforeUpdateCount;\n" +
            "public void Handle()\n" +
            "{\n" +
            "    \n" +
            "}\n" +
            "}\n";
        var service = CreateService(source, ScriptSemanticFixture.CreateContext());

        var completions = service.GetCompletions(DocumentUri, PositionAfter(source, "{\n    "));

        completions.ShouldContain(completion => completion.Label == "_beforeUpdateCount");
    }

    [Fact]
    public void GetCompletions_MemberDeclarationPosition_StillOffersOwnPrivateMembers()
    {
        // The adjacent context, decided deliberately: a class-body line is also where a field
        // initializer begins (`private int _total = _beforeUpdateCount + 1;`), so the service
        // cannot narrow it to keywords and types without breaking the initializer it also is.
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "private int _beforeUpdateCount;\n" +
            "    \n" +
            "}\n";
        var service = CreateService(source, ScriptSemanticFixture.CreateContext());

        var completions = service.GetCompletions(
            DocumentUri,
            PositionAfter(source, "_beforeUpdateCount;\n    "));

        completions.ShouldContain(completion => completion.Label == "_beforeUpdateCount");
        completions.ShouldContain(completion => completion.Label == "Context");
        completions.ShouldContain(completion => completion.Label == "using");
    }

    private static LanguagePosition UsingDirectivePosition()
        => PositionAfter(ComponentSource, "using \n", trailingAdjustment: -1);

    private static IViuLanguageService CreateService(string source, LanguageProjectContext? context)
    {
        var service = ViuLanguageServices.Create();
        if (context is not null)
        {
            ((IScriptSemanticLanguageService)service).ConfigureProjectContext(DocumentUri, context);
        }

        service.OpenDocument(DocumentUri, source, 1);
        return service;
    }

    private static LanguagePosition PositionAfter(
        string source,
        string marker,
        int trailingAdjustment = 0)
    {
        var offset = source.IndexOf(marker, StringComparison.Ordinal) +
            marker.Length +
            trailingAdjustment;
        return TextCoordinateConverter.GetPosition(source, offset);
    }
}
