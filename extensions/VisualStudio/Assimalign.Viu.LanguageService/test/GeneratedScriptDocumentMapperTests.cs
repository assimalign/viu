using System;
using System.Threading;

using Shouldly;

using Xunit;

using Assimalign.Viu.Tooling.Css;
using Assimalign.Viu.Tooling.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Pins the affine simple-form <c>#line</c> region map ([V01.01.12.23], #259) against the REAL
/// emitter output: file positions inside the hoisted-using and merged-member regions round-trip in
/// both directions, while template positions, the scaffold, and the render body's span-form-mapped
/// lines are suppressed — never misplaced.
/// </summary>
public class GeneratedScriptDocumentMapperTests
{
    private const string FilePath = ScriptSemanticFixture.DocumentFilePath;

    private const string Source =
        "<template>\n" +
        "  <div>{{ Count }}</div>\n" +
        "</template>\n" +
        "@script {\n" +
        "using System;\n" +
        "\n" +
        "public int Count { get; set; }\n" +
        "}\n";

    [Fact]
    public void TryMapFileOffsetToGenerated_MemberRegionIdentifier_RoundTripsBothDirections()
    {
        var generated = EmitGeneratedDocument(Source);
        var mapper = GeneratedScriptDocumentMapper.Create(Source, generated, FilePath);
        var fileOffset =
            Source.IndexOf("public int Count", StringComparison.Ordinal) + "public int ".Length;

        mapper.TryMapFileOffsetToGenerated(fileOffset, out var generatedOffset).ShouldBeTrue();
        generated.Substring(generatedOffset, "Count".Length).ShouldBe("Count");

        mapper.TryMapGeneratedSpanToFile(
                generatedOffset,
                "Count".Length,
                out var mappedStart,
                out var mappedLength)
            .ShouldBeTrue();
        mappedStart.ShouldBe(fileOffset);
        mappedLength.ShouldBe("Count".Length);
    }

    [Fact]
    public void TryMapFileOffsetToGenerated_HoistedUsingRegion_RoundTripsBothDirections()
    {
        var generated = EmitGeneratedDocument(Source);
        var mapper = GeneratedScriptDocumentMapper.Create(Source, generated, FilePath);
        var fileOffset = Source.IndexOf("System;", StringComparison.Ordinal);

        mapper.TryMapFileOffsetToGenerated(fileOffset, out var generatedOffset).ShouldBeTrue();
        generated.Substring(generatedOffset, "System".Length).ShouldBe("System");

        mapper.TryMapGeneratedSpanToFile(
                generatedOffset,
                "System".Length,
                out var mappedStart,
                out _)
            .ShouldBeTrue();
        mappedStart.ShouldBe(fileOffset);
    }

    [Fact]
    public void TryMapFileOffsetToGenerated_TemplatePosition_IsUnmapped()
    {
        var generated = EmitGeneratedDocument(Source);
        var mapper = GeneratedScriptDocumentMapper.Create(Source, generated, FilePath);

        mapper.TryMapFileOffsetToGenerated(
                Source.IndexOf("{{", StringComparison.Ordinal),
                out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void TryMapFileOffsetToGenerated_ScriptClosingBraceLine_IsUnmapped()
    {
        var generated = EmitGeneratedDocument(Source);
        var mapper = GeneratedScriptDocumentMapper.Create(Source, generated, FilePath);

        // The member region's verbatim content ends before the block's closing-brace line.
        mapper.TryMapFileOffsetToGenerated(
                Source.LastIndexOf('}'),
                out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void TryMapGeneratedSpanToFile_ScaffoldSpan_IsSuppressed()
    {
        var generated = EmitGeneratedDocument(Source);
        var mapper = GeneratedScriptDocumentMapper.Create(Source, generated, FilePath);
        var scaffoldIndex = generated.IndexOf("partial class", StringComparison.Ordinal);
        scaffoldIndex.ShouldBeGreaterThanOrEqualTo(0);

        mapper.TryMapGeneratedSpanToFile(scaffoldIndex, "partial".Length, out _, out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void TryMapGeneratedSpanToFile_RenderBodySpanFormLine_IsSuppressed()
    {
        // The render body is mapped by the distinct span-form directive, which the mapper
        // deliberately does not match — a span on its following line must be suppressed.
        var generated = EmitGeneratedDocument(Source);
        var mapper = GeneratedScriptDocumentMapper.Create(Source, generated, FilePath);
        var spanDirectiveIndex = generated.IndexOf("#line (", StringComparison.Ordinal);
        spanDirectiveIndex.ShouldBeGreaterThanOrEqualTo(0);
        var followingLineStart = generated.IndexOf('\n', spanDirectiveIndex) + 1;

        mapper.TryMapGeneratedSpanToFile(followingLineStart, 1, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryMapFileOffsetToGenerated_ComponentWithoutScript_IsUnmapped()
    {
        const string templateOnly = "<template>\n  <div>x</div>\n</template>\n";
        var generated = EmitGeneratedDocument(templateOnly);
        var mapper = GeneratedScriptDocumentMapper.Create(templateOnly, generated, FilePath);

        mapper.TryMapFileOffsetToGenerated(5, out _).ShouldBeFalse();
    }

    private static string EmitGeneratedDocument(string fileText)
    {
        var name = SingleFileComponentNameResolver.Resolve(
            FilePath,
            ScriptSemanticFixture.ProjectDirectory,
            ScriptSemanticFixture.RootNamespace);
        var input = new SingleFileComponentProjectionInput(
            SingleFileComponentFormat.Viu,
            FilePath,
            "AppShell.viu",
            fileText,
            name.Namespace,
            name.ClassName,
            name.HintName,
            StyleScopeId.Resolve(FilePath, ScriptSemanticFixture.ProjectDirectory),
            HotReloadComponentIdentifier: null,
            HasCanonicalPeer: false);
        var projection = SingleFileComponentProjection.Project(input, CancellationToken.None);
        return SingleFileComponentSourceEmitter.Emit(projection.Model);
    }
}
