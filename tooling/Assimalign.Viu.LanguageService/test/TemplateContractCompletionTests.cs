using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins template vocabulary and contract completion ([V01.01.12.07.12], #333), including the
/// [SFC-CG-8] authored-case precedence shared with [SFC-USE-5] identity resolution.
/// </summary>
public class TemplateContractCompletionTests
{
    private const string ButtonComponentSource =
        "<template>\n" +
        "  <button>{{ Label }}</button>\n" +
        "</template>\n" +
        "@script {\n" +
        "    using Assimalign.Viu.Components;\n" +
        "\n" +
        "    [Parameter(IsRequired = true)]\n" +
        "    public string Label { get; set; } = string.Empty;\n" +
        "\n" +
        "    [Event]\n" +
        "    public partial void Submitted(string value);\n" +
        "}\n";

    [Fact]
    public void GetCompletions_DirectiveName_OffersSupportedDirectiveVocabulary()
    {
        const string source = "<template>\n  <div v-></div>\n</template>\n";
        var completions = CompleteAfter(source, "<div v-");

        completions.ShouldContain(item => item.Label == "v-if");
        completions.ShouldContain(item => item.Label == "v-for");
        completions.ShouldContain(item => item.Label == "v-model");
    }

    [Fact]
    public void GetCompletions_NativeEventName_OffersEventVocabulary()
    {
        const string source = "<template>\n  <button @key></button>\n</template>\n";
        var completions = CompleteAfter(source, "<button @key");

        completions.ShouldContain(item => item.Label == "@keydown");
        completions.ShouldContain(item => item.Label == "@keyup");
    }

    [Fact]
    public void GetCompletions_UnresolvedLowercaseStaticTag_ReturnsNoNativeEvents()
    {
        const string source =
            "<template>\n  <feature-card @key></feature-card>\n</template>\n";

        CompleteAfter(source, "<feature-card @key").ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_LowercaseNativeButton_OffersNativeAttributes()
    {
        const string source = "<template>\n  <button :ty></button>\n</template>\n";
        var completions = CompleteAfter(source, "<button :ty");

        completions.ShouldContain(item => item.Label == ":type");
        completions.ShouldNotContain(item => item.Label == ":label");
    }

    [Fact]
    public void GetCompletions_PascalCaseButtonCollision_OffersComponentParametersNotNativeAttributes()
    {
        const string source = "<template>\n  <Button :></Button>\n</template>\n";
        var service = CreateServiceWithButtonComponent();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var completions = service.GetCompletions(
            ScriptSemanticFixture.DocumentUri,
            PositionAfter(source, "<Button :"));

        var label = completions.Single(item => item.Label == ":label");
        label.Detail.ShouldBe("string component parameter");
        label.Documentation.ShouldContain("required");
        completions.ShouldNotContain(item => item.Label == ":type");
        completions.ShouldNotContain(item => item.Label == ":disabled");
    }

    [Fact]
    public void GetCompletions_ComponentEventName_OffersDeclaredEventSnippet()
    {
        const string source = "<template>\n  <Button @sub></Button>\n</template>\n";
        var service = CreateServiceWithButtonComponent();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var completion = service.GetCompletions(
                ScriptSemanticFixture.DocumentUri,
                PositionAfter(source, "<Button @sub"))
            .Single(item => item.Label == "@submitted");

        completion.Kind.ShouldBe(LanguageCompletionItemKind.Snippet);
        completion.IsSnippet.ShouldBeTrue();
        completion.InsertText.ShouldBe("@submitted=\"$1\"");
        completion.Detail.ShouldContain("Submitted(string value)");
    }

    [Fact]
    public void GetCompletions_TemplateHtmlComment_ReturnsNoCompletions()
    {
        const string source =
            "<template>\n" +
            "  <!-- <div class=\"gap-\" v-if=\"Visible\"></div> -->\n" +
            "</template>\n";

        CompleteAfter(source, "gap-").ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_StyleCssComment_ReturnsNoCompletions()
    {
        const string source =
            "<style>\n" +
            "  /* dis */\n" +
            "</style>\n";

        CompleteAfter(source, "dis").ShouldBeEmpty();
    }

    private static System.Collections.Generic.IReadOnlyList<LanguageCompletionItem> CompleteAfter(
        string source,
        string marker)
    {
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);
        return service.GetCompletions(
            ScriptSemanticFixture.DocumentUri,
            PositionAfter(source, marker));
    }

    private static ILanguageService CreateServiceWithButtonComponent()
    {
        var service = LanguageServices.Create();
        service.ShouldBeAssignableTo<IScriptSemanticLanguageService>()
            .ConfigureProjectContext(
                ScriptSemanticFixture.DocumentUri,
                ScriptSemanticFixture.CreateContext(
                    new LanguageProjectSourceDocument(
                        "C:/workspace/App/Button.viu",
                        ButtonComponentSource,
                        IsComponent: true)));
        return service;
    }

    private static LanguagePosition PositionAfter(string source, string marker)
    {
        var markerOffset = source.IndexOf(marker, StringComparison.Ordinal);
        markerOffset.ShouldBeGreaterThanOrEqualTo(0);
        return TextCoordinateConverter.GetPosition(source, markerOffset + marker.Length);
    }
}
