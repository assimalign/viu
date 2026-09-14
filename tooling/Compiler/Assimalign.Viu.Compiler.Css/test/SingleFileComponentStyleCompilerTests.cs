using Assimalign.Viu.Compiler.Css;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Compiler.Css.Tests;

/// <summary>
/// Pins the shared <see cref="SingleFileComponentStyleCompiler"/> — the deterministic style
/// compilation both the generator and the <c>ViuBundleCss</c> task run ([V01.01.12.12]). These tests fix
/// the exact compiled CSS so any drift (which would break the byte-identical-to-generated-constant contract)
/// fails here.
/// </summary>
public sealed class SingleFileComponentStyleCompilerTests
{
    private const string ProjectDirectory = "C:/proj";

    private static SingleFileComponentStyleCompilation Compile(string text, string filePath)
        => SingleFileComponentStyleCompiler.CompileFile(
            SingleFileComponentParserFactory.CreateForStyleExtraction(), text, filePath, ProjectDirectory);

    /// <summary>A component with no style block compiles to the empty result.</summary>
    [Fact]
    public void Compile_NoStyleBlock_ReturnsEmpty()
    {
        var result = Compile("<template>\n    <div>ok</div>\n</template>\n", "C:/proj/Components/NoStyle.viu");

        result.ShouldBe(SingleFileComponentStyleCompilation.Empty);
        result.ExtractedStyles.ShouldBeNull();
    }

    /// <summary>An ordinary block without modules or bindings passes through verbatim.</summary>
    [Fact]
    public void Compile_PlainBlock_PassesThroughVerbatim()
    {
        var result = Compile("<style>\n    .a { color: red; }\n</style>\n", "C:/proj/Components/Plain.viu");

        result.ExtractedStyles.ShouldNotBeNull();
        result.ExtractedStyles!.ShouldContain(".a { color: red; }");
        result.ExtractedStyles!.ShouldNotContain("data-v-");
    }

    [Fact]
    public void Compile_ModuleAndBinding_PreservesNamesAfterScopedCssRemoval()
    {
        // [V01.01.06.17] Retain the pre-removal module and binding hashes for the same component path.
        const string path = "C:/proj/Components/Card.viu";
        var result = Compile("<style module>\n.box { color: v-bind(color); }\n</style>\n", path);

        CssComponentHash.Resolve(path, ProjectDirectory).ShouldBe("0e7c3b3e");
        result.ModuleClasses.ShouldHaveSingleItem().Hashed.ShouldBe("box_3f13189d");
        result.ExtractedStyles.ShouldBe(".box_3f13189d {\n  color: var(--c22325db);\n}\n");
        result.VariableBindings.Count.ShouldBe(1);
        result.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>Compilation is deterministic: identical input yields the identical string.</summary>
    [Fact]
    public void Compile_IsDeterministic()
    {
        const string path = "C:/proj/Components/Card.viu";
        const string text = "<style>\n    .card { color: red; padding: 8px; }\n</style>\n";

        Compile(text, path).ExtractedStyles.ShouldBe(Compile(text, path).ExtractedStyles);
    }
}
