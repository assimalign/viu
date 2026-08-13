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
    public void GetCompletions_ComponentEventName_OffersTheDeclaredEvent()
    {
        const string source = "<template>\n  <Button @sub></Button>\n</template>\n";
        var service = CreateServiceWithButtonComponent();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var completion = service.GetCompletions(
                ScriptSemanticFixture.DocumentUri,
                PositionAfter(source, "<Button @sub"))
            .Single(item => item.Label == "@submitted");

        // The item writes its own empty value: a snippet-capable client puts the caret in it from
        // the tabstop, and Visual Studio's commit adapter places it there for a client that is not.
        completion.InsertText.ShouldBe("@submitted=\"$1\"");
        completion.IsSnippet.ShouldBeTrue();
        completion.Detail.ShouldContain("Submitted(string value)");
    }

    [Fact]
    public void GetCompletions_ComponentAttributeName_OffersParametersStaticallyAndBound()
    {
        // A parameter takes a static value as readily as a bound one, so both forms are offered. The
        // colon spelling was the only one before, which hid half the surface from anyone who had not
        // already typed one.
        const string source = "<template>\n  <Button ></Button>\n</template>\n";
        var service = CreateServiceWithButtonComponent();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var completions = service.GetCompletions(
            ScriptSemanticFixture.DocumentUri,
            PositionAfter(source, "<Button "));

        var staticParameter = completions.Single(item => item.Label == "label");
        staticParameter.InsertText.ShouldBe("label=\"$1\"");
        staticParameter.Detail.ShouldBe("string component parameter");
        completions.ShouldContain(item => item.Label == ":label");
        completions.ShouldContain(item => item.Label == "@submitted");
        completions.ShouldContain(item => item.Label == "v-if");
        // The native vocabulary still stays out of a component's attribute area ([SFC-CG-8]).
        completions.ShouldNotContain(item => item.Label == "type");
    }

    [Fact]
    public void GetCompletions_NativeAttributeName_OffersAttributesHandlersAndDirectives()
    {
        const string source = "<template>\n  <button ></button>\n</template>\n";
        var completions = CompleteAfter(source, "<button ");

        completions.ShouldContain(item => item.Label == "type");
        completions.ShouldContain(item => item.Label == ":type");
        completions.ShouldContain(item => item.Label == "@click");
        completions.ShouldContain(item => item.Label == "v-if");
    }

    [Theory]
    // The reported defect: the shorthand is punctuation, so an editor inferring the replaced span
    // from the typed word left it in place and the committed name landed beside it — ::label, @@sub.
    [InlineData("<Button :", ":label")]
    [InlineData("<Button @", "@submitted")]
    [InlineData("<Button v-i", "v-if")]
    public void GetCompletions_AttributeName_ReplacesTheShorthandItWasTriggeredBy(
        string marker,
        string label)
    {
        var source = $"<template>\n  {marker}></Button>\n</template>\n";
        var service = CreateServiceWithButtonComponent();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);
        var position = PositionAfter(source, marker);
        var typedName = marker.Substring(marker.LastIndexOf(' ') + 1);

        var completion = service
            .GetCompletions(ScriptSemanticFixture.DocumentUri, position)
            .Single(item => item.Label == label);

        completion.EditRange.ShouldBe(
            new LanguageRange(
                new LanguagePosition(position.Line, position.Character - typedName.Length),
                position));
    }

    [Fact]
    public void GetCompletions_LongFormBindingPrefix_OffersTheSameSurfaceAsTheShorthand()
    {
        const string source = "<template>\n  <Button v-bind:></Button>\n</template>\n";
        var service = CreateServiceWithButtonComponent();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var completion = service
            .GetCompletions(
                ScriptSemanticFixture.DocumentUri,
                PositionAfter(source, "<Button v-bind:"))
            .Single(item => item.Label == "v-bind:label");

        completion.InsertText.ShouldBe("v-bind:label=\"$1\"");
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

    [Fact]
    public void GetCompletions_NodePosition_OffersTheComponentsThisCompilationResolves()
    {
        // A node position names a component from the same [SFC-USE-5] catalog the compiler resolves
        // usages against, so a sibling the author just wrote is offered without registering it
        // anywhere — and it is offered as a type, not as an element.
        const string source = "<template>\n  <\n</template>\n";
        var service = CreateServiceWithButtonComponent();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var completions = service.GetCompletions(
            ScriptSemanticFixture.DocumentUri,
            PositionAfter(source, "  <"));

        var component = completions.Single(item => item.Label == "Button");
        component.Kind.ShouldBe(LanguageCompletionItemKind.Class);
        component.InsertText.ShouldBe("<Button");
        // A component sorts above the native element vocabulary: it is the author's own code.
        component.SortText.ShouldBe("10:Button");
        completions.Single(item => item.Label == "div").SortText.ShouldBe("30:div");
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
