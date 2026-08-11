using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins projected <c>@script</c> identifier classifications against the identical plain-C# syntax
/// bound by the same references. Each reported W5.7 construct must carry the same C# editor
/// classification-type name on both sides of the projection. [V01.01.12.07.11]
/// </summary>
public class ScriptSemanticClassificationTests
{
    private const string VueDocumentUri = "file:///C:/workspace/App/AppShell.vue";

    private const string ScriptBody =
        "using Assimalign.Viu.Components;\n" +
        "[Parameter(IsRequired = true)]\n" +
        "public string Title { get; set; } = string.Empty;\n" +
        "private void Navigate(string path)\n" +
        "{\n" +
        "    var local = Title + path;\n" +
        "    Title = local;\n" +
        "}\n";

    private const string ComponentSource =
        "<template>\n" +
        "  <div>{{ Title }}</div>\n" +
        "</template>\n" +
        "@script {\n" +
        ScriptBody +
        "}\n";

    private static readonly string PlainSource =
        ScriptBody[.."using Assimalign.Viu.Components;\n".Length] +
        "public sealed class PlainComponent\n" +
        "{\n" +
        ScriptBody["using Assimalign.Viu.Components;\n".Length..] +
        "}\n";

    public static IEnumerable<object[]> ReportedConstructs()
    {
        yield return Case("using Assimalign", "Assimalign", LanguageClassificationTypeNames.NamespaceName);
        yield return Case("[Parameter(", "Parameter", LanguageClassificationTypeNames.ClassName);
        yield return Case("IsRequired =", "IsRequired", LanguageClassificationTypeNames.PropertyName);
        yield return Case("Title { get", "Title", LanguageClassificationTypeNames.PropertyName);
        yield return Case("Empty;", "Empty", LanguageClassificationTypeNames.FieldName);
        yield return Case("string path)", "path", LanguageClassificationTypeNames.ParameterName);
        yield return Case("Title + path", "Title", LanguageClassificationTypeNames.PropertyName);
        yield return Case("Title + path", "path", LanguageClassificationTypeNames.ParameterName);
        yield return Case("var local =", "local", LanguageClassificationTypeNames.LocalName);
        yield return Case("Title = local", "local", LanguageClassificationTypeNames.LocalName);
    }

    [Theory]
    [MemberData(nameof(ReportedConstructs))]
    public void GetClassifications_ProjectedConstruct_MatchesPlainCSharpBaseline(
        string marker,
        string token,
        string expectedClassificationTypeName)
    {
        var service = LanguageServices.Create();
        ((IScriptSemanticLanguageService)service).ConfigureProjectContext(
            ScriptSemanticFixture.DocumentUri,
            ScriptSemanticFixture.CreateContext());
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, ComponentSource, 1);

        var projected = service.GetClassifications(ScriptSemanticFixture.DocumentUri);
        var projectedStart = FindTokenStart(ComponentSource, marker, token);
        var projectedTypeName = FindAuthoredClassification(
            ComponentSource,
            projected,
            projectedStart);
        var plainTypeName = FindPlainClassification(PlainSource, marker, token);

        plainTypeName.ShouldBe(expectedClassificationTypeName);
        projectedTypeName.ShouldBe(plainTypeName);
    }

    [Fact]
    public void GetClassifications_GeneratedScaffold_IsNeverReturnedAtAuthoredPositions()
    {
        var service = LanguageServices.Create();
        ((IScriptSemanticLanguageService)service).ConfigureProjectContext(
            ScriptSemanticFixture.DocumentUri,
            ScriptSemanticFixture.CreateContext());
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, ComponentSource, 1);

        var classifications = service.GetClassifications(ScriptSemanticFixture.DocumentUri);

        classifications.ShouldAllBe(classification =>
            classification.Range.Start.Line >= 4 && classification.Range.Start.Line <= 11);
    }

    [Fact]
    public void GetClassifications_VueScriptSetupOnly_ClassifiesAuthoredCSharp()
    {
        const string source =
            "<script setup lang=\"csharp\">\n" +
            "public int Count { get; set; }\n" +
            "</script>\n";
        var service = LanguageServices.Create();
        ((IScriptSemanticLanguageService)service).ConfigureProjectContext(
            VueDocumentUri,
            ScriptSemanticFixture.CreateContext());
        service.OpenDocument(VueDocumentUri, source, 1);

        var classifications = service.GetClassifications(VueDocumentUri);
        var countStart = source.IndexOf("Count", StringComparison.Ordinal);

        FindAuthoredClassification(source, classifications, countStart)
            .ShouldBe(LanguageClassificationTypeNames.PropertyName);
    }

    [Fact]
    public void GetClassificationSnapshot_DocumentChange_PublishesMatchingVersionTextAndExactNames()
    {
        var service = LanguageServices.Create();
        ((IScriptSemanticLanguageService)service).ConfigureProjectContext(
            ScriptSemanticFixture.DocumentUri,
            ScriptSemanticFixture.CreateContext());
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, ComponentSource, 1);

        LanguageClassificationSnapshot? first =
            service.GetClassificationSnapshot(ScriptSemanticFixture.DocumentUri);
        const string updatedSource =
            "@script {\n" +
            "public string Name { get; set; } = string.Empty;\n" +
            "}\n";
        service.ChangeDocument(
                ScriptSemanticFixture.DocumentUri,
                2,
                [new LanguageDocumentChange(null, updatedSource)])
            .ShouldBeTrue();
        LanguageClassificationSnapshot? second =
            service.GetClassificationSnapshot(ScriptSemanticFixture.DocumentUri);

        first.ShouldNotBeNull();
        first.Version.ShouldBe(1);
        first.TextChecksum.ShouldBe(LanguageTextChecksum.Compute(ComponentSource));
        first.Classifications.ShouldContain(classification =>
            classification.ClassificationTypeName == LanguageClassificationTypeNames.PropertyName);
        second.ShouldNotBeNull();
        second.Version.ShouldBe(2);
        second.TextChecksum.ShouldBe(LanguageTextChecksum.Compute(updatedSource));
        second.TextChecksum.ShouldNotBe(first.TextChecksum);
        second.Classifications.ShouldContain(classification =>
            classification.ClassificationTypeName == LanguageClassificationTypeNames.PropertyName);
    }

    [Fact]
    public void LanguageTextChecksum_KnownText_UsesUtf8Sha256TransportIdentity()
    {
        LanguageTextChecksum.Compute("Viu\n")
            .ShouldBe("cky5hMd4dzR9iJUWww+rP9qM9jfQl0FUB90suepnndI=");
    }

    private static object[] Case(string marker, string token, string expected)
        => [marker, token, expected];

    private static string FindAuthoredClassification(
        string source,
        IReadOnlyList<LanguageClassification> classifications,
        int tokenStart)
    {
        return classifications.Single(classification =>
        {
            TextCoordinateConverter.TryGetOffset(
                    source,
                    classification.Range.Start,
                    out var classificationStart)
                .ShouldBeTrue();
            return classificationStart == tokenStart;
        }).ClassificationTypeName;
    }

    private static string FindPlainClassification(
        string source,
        string marker,
        string token)
    {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview));
        var references = ScriptSemanticFixture.ReferenceAssemblyPaths()
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "PlainClassificationBaseline",
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var classifications = ScriptSemanticClassifier.Classify(
            compilation.GetSemanticModel(tree),
            tree.GetRoot(),
            CancellationToken.None);
        var tokenStart = FindTokenStart(source, marker, token);
        return classifications.Single(classification => classification.Span.Start == tokenStart)
            .ClassificationTypeName;
    }

    private static int FindTokenStart(string source, string marker, string token)
    {
        var markerStart = source.IndexOf(marker, StringComparison.Ordinal);
        markerStart.ShouldBeGreaterThanOrEqualTo(0);
        var tokenStart = source.IndexOf(token, markerStart, marker.Length, StringComparison.Ordinal);
        tokenStart.ShouldBeGreaterThanOrEqualTo(markerStart);
        return tokenStart;
    }
}
