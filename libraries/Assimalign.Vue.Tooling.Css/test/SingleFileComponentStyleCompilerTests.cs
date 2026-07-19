using Assimalign.Vue.Tooling.Css;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Tooling.Css.Tests;

/// <summary>
/// Pins the shared <see cref="SingleFileComponentStyleCompiler"/> — the deterministic <c>@style</c>
/// compilation both the generator and the <c>VuecsBundleCss</c> task run ([V01.01.12.12]). These tests fix
/// the exact compiled CSS so any drift (which would break the byte-identical-to-generated-constant contract)
/// fails here.
/// </summary>
public sealed class SingleFileComponentStyleCompilerTests
{
    private const string ProjectDirectory = "C:/proj";

    private static SingleFileComponentStyleCompilation Compile(string text, string filePath)
        => SingleFileComponentStyleCompiler.CompileFile(
            SingleFileComponentParserFactory.CreateForStyleExtraction(), text, filePath, ProjectDirectory);

    /// <summary>A <c>scoped</c> block rewrites its selector with the component's stable scope id, canonically.</summary>
    [Fact]
    public void Compile_ScopedBlock_RewritesSelectorWithScopeIdCanonically()
    {
        const string path = "C:/proj/Components/Card.viu";
        var scopeId = StyleScopeId.Resolve(path, ProjectDirectory);

        var result = Compile("@style scoped {\n    .card { color: red; }\n}\n", path);

        result.ScopeId.ShouldBe(scopeId);
        result.ExtractedStyles.ShouldBe($".card[{scopeId}] {{\n  color: red;\n}}\n");
    }

    /// <summary>A component with no <c>@style</c> block compiles to the empty result.</summary>
    [Fact]
    public void Compile_NoStyleBlock_ReturnsEmpty()
    {
        var result = Compile("@template {\n    <div>ok</div>\n}\n", "C:/proj/Components/NoStyle.viu");

        result.ShouldBe(SingleFileComponentStyleCompilation.Empty);
        result.ExtractedStyles.ShouldBeNull();
        result.ScopeId.ShouldBeNull();
    }

    /// <summary>A non-scoped, non-module, non-v-bind block passes through verbatim (no scope id stamped).</summary>
    [Fact]
    public void Compile_PlainBlock_PassesThroughVerbatim()
    {
        var result = Compile("@style {\n    .a { color: red; }\n}\n", "C:/proj/Components/Plain.viu");

        result.ScopeId.ShouldBeNull();
        result.ExtractedStyles.ShouldNotBeNull();
        result.ExtractedStyles!.ShouldContain(".a { color: red; }");
        result.ExtractedStyles!.ShouldNotContain("data-v-");
    }

    /// <summary>Compilation is deterministic: identical input yields the identical string.</summary>
    [Fact]
    public void Compile_IsDeterministic()
    {
        const string path = "C:/proj/Components/Card.viu";
        const string text = "@style scoped {\n    .card { color: red; padding: 8px; }\n}\n";

        Compile(text, path).ExtractedStyles.ShouldBe(Compile(text, path).ExtractedStyles);
    }
}
