using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.Templates;

using Shouldly;
using Xunit;

// Both the Viu syntax cluster and Roslyn expose Diagnostic and DiagnosticSeverity; alias the Roslyn
// types that the generated code compiles against.
using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Tests for [V01.01.05.08] — the render-body source map: the compiled <c>@template</c> render function is
/// wrapped in <c>#line</c> span directives so a C# compile error inside an emitted template expression
/// resolves to the offending <c>.viu</c> template line and column, never to opaque generated code. This is
/// the render-body analogue of the <c>@script</c> merge's <c>#line</c> map (see
/// <see cref="SingleFileComponentScriptTests"/>), and it is proved the same way: through the real C#
/// compiler, asserting <c>GetMappedLineSpan()</c>. Upstream analogue: the <c>SourceMapGenerator</c> output
/// of <c>@vue/compiler-core</c>'s <c>generate()</c> (<c>codegen.ts</c>), adapted to the Roslyn <c>#line</c>
/// mechanism because Viu diagnostics travel through the C# compiler.
/// </summary>
public sealed class SingleFileComponentTemplateSourceMapTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";

    [Fact]
    public void TemplateExpressionError_ResolvesToViuTemplateLineAndColumn_ViaLineDirective()
    {
        // An unknown member inside a template interpolation is emitted as the recoverable _ctx.Cont
        // fallback under permissive (default) binding metadata — no generate-time diagnostic. The SEMANTIC
        // error surfaces only when the generated render body is compiled: Counter has no `Cont` member, so
        // `_ctx.Cont` is CS1061. The emitted #line span map must resolve it to the .viu template — file
        // line 2 (zero-based 1), the exact column of `Cont` — never to the .g.cs.
        const string source =
            "@template {\n" +               // line 1
            "<div>{{ Cont }}</div>\n" +     // line 2: `Cont` begins at column 9 (zero-based char 8)
            "}\n";                          // line 3

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Counter.viu", source, RootNamespace, ProjectDirectory);
        outcome.Diagnostics.ShouldBeEmpty(); // permissive metadata: the unresolved identifier is not reported at generate time
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Counter.SingleFileComponent.g.cs");

        var conversion = CompileErrors(CompileGeneratedWithHelperStub(generated))
            .ShouldHaveSingleItem();
        conversion.Id.ShouldBe("CS1061"); // 'Counter' does not contain a definition for 'Cont'
        // GetMappedLineSpan honors the emitted #line span directive (GetLineSpan would give the physical
        // .g.cs location); the mapped span is what an IDE squiggle and a CLI build resolve to.
        var span = conversion.Location.GetMappedLineSpan();
        span.Path.ShouldEndWith("Counter.viu");
        span.StartLinePosition.Line.ShouldBe(1);       // .viu file line 2, zero-based
        span.StartLinePosition.Character.ShouldBe(8);  // the `Cont` column, mapped exactly through the span directive
    }

    [Fact]
    public void TemplateExpressionError_OnAnIndentedTemplateLine_StillLandsAtExactColumn()
    {
        // Indentation shifts the template column, and the emitted expression sits at an unrelated generated
        // column (past the inserted `_ctx.` prefix and the enclosing helper calls); the span directive's
        // character offset is what re-aligns them, so the mapped column tracks the template, not the .g.cs.
        const string source =
            "@template {\n" +
            "        <span>{{ Missing }}</span>\n" +  // `Missing` begins at column 18 (zero-based char 17)
            "}\n";

        var generated = GeneratorTestHarness.GeneratedSource(
            GeneratorTestHarness.Run($"{ProjectDirectory}/Widget.viu", source, RootNamespace, ProjectDirectory),
            "Widget.SingleFileComponent.g.cs");

        var conversion = CompileErrors(CompileGeneratedWithHelperStub(generated)).ShouldHaveSingleItem();
        conversion.Id.ShouldBe("CS1061");
        var span = conversion.Location.GetMappedLineSpan();
        span.Path.ShouldEndWith("Widget.viu");
        span.StartLinePosition.Line.ShouldBe(1);        // .viu file line 2
        span.StartLinePosition.Character.ShouldBe(17);  // the `Missing` column, preserved through the offset
    }

    [Fact]
    public void RenderBody_WrapsEachExpressionLine_InALineSpanDirectiveClosedByDefault()
    {
        // The emitted render body carries the directive pair around the expression-bearing line, exactly as
        // the @script seam does around merged script — a lightweight structural pin that does not require a
        // compile.
        const string source =
            "@template {\n" +
            "<p>{{ label }}</p>\n" +
            "}\n";

        var generated = GeneratorTestHarness.GeneratedSource(
            GeneratorTestHarness.Run($"{ProjectDirectory}/Tag.viu", source, RootNamespace, ProjectDirectory),
            "Tag.SingleFileComponent.g.cs");

        // `label` is at file line 2, column 7 (one-based) through column 12 (its 5-char span end).
        generated.ShouldContain("#line (2,7)-(2,12) ");
        generated.ShouldContain("\"C:/proj/Tag.viu\"");
        generated.ShouldContain("_toDisplayString(_ctx.label)");
        generated.ShouldContain("#line default");
    }

    [Fact]
    public void StaticOnlyTemplate_EmitsNoLineDirectives()
    {
        // A template with no dynamic expressions has nothing to map, so the render body is emitted with no
        // #line directives at all (the render source map is empty).
        const string source =
            "@template {\n" +
            "<div>static text</div>\n" +
            "}\n";

        var generated = GeneratorTestHarness.GeneratedSource(
            GeneratorTestHarness.Run($"{ProjectDirectory}/Plain.viu", source, RootNamespace, ProjectDirectory),
            "Plain.SingleFileComponent.g.cs");

        // The @script seam's #line map is absent (no script); the render body adds none of its own.
        generated.ShouldNotContain("#line (");
    }

    [Fact]
    public void RenderSourceMap_IdenticalInput_StaysStrictlyCached()
    {
        // The render source map rides inside the value-equatable model, so a template that produces
        // mappings must still cache: identical input re-runs to an equal model (equal RenderBody with equal
        // injected directives), leaving the model step strictly Cached, not Unchanged.
        const string source =
            "@template {\n" +
            "<div :id=\"dynamicId\">{{ message }}</div>\n" +
            "}\n";

        var file = new InMemoryAdditionalText($"{ProjectDirectory}/Counter.viu", source);
        var compilation = GeneratorTestHarness.CreateCompilation();
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(file), RootNamespace, ProjectDirectory);

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);

        driver.GetRunResult().Results[0]
            .TrackedSteps[SingleFileComponentGenerator.ModelTrackingName]
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Mapper_AnchorsEachGeneratedLineToItsLeftmostExpression_AndClosesWithDefault()
    {
        // Unit-level pin on RenderBodySourceMapper: two mappings on ONE generated line collapse to a single
        // directive anchored at the leftmost (smallest generated column), since only one #line directive can
        // lead a physical line; the mapping is composed onto the block's content-start position.
        var body = "        return _f(_ctx.a, _ctx.b);\n";
        var blockStart = new Position(Offset: 12, Line: 2, Column: 1); // @template content starts on file line 2
        var mappings = new Assimalign.Viu.Syntax.SyntaxList<RenderSourceMapping>(new[]
        {
            // `b` further right on the same generated line (line 0), template col 10.
            new RenderSourceMapping
            {
                GeneratedLine = 0,
                GeneratedColumn = 26,
                TemplateLocation = Loc(1, 10, 1, 11, "b"),
            },
            // `a` earlier on the same line, template col 5 — the leftmost, so it anchors.
            new RenderSourceMapping
            {
                GeneratedLine = 0,
                GeneratedColumn = 20,
                TemplateLocation = Loc(1, 5, 1, 6, "a"),
            },
        });

        var injected = RenderBodySourceMapper.Inject(body, mappings, blockStart, "C:/proj/X.viu");

        // The block content starts at file line 2 column 1, so a content-relative (line 1, col 5) composes
        // to file (line 2, col 5); the anchor is `a` (generated column 20), not `b`.
        injected.ShouldBe(
            "#line (2,5)-(2,6) 20 \"C:/proj/X.viu\"\n" +
            "        return _f(_ctx.a, _ctx.b);\n" +
            "#line default\n");
    }

    [Fact]
    public void Mapper_WithNoMappings_ReturnsBodyUnchanged()
    {
        const string body = "        return null;\n";
        RenderBodySourceMapper.Inject(
                body,
                Assimalign.Viu.Syntax.SyntaxList<RenderSourceMapping>.Empty,
                new Position(0, 1, 1),
                "C:/proj/X.viu")
            .ShouldBe(body);
    }

    [Fact]
    public void GeneratedComponentTemplateBridge_WithOnSetupImplementation_CompilesAgainstApprovedContract()
    {
        const string source =
            "@template {\n" +
            "    <div>ready</div>\n" +
            "}\n" +
            "@script {\n" +
            "    partial void OnSetup() { }\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/SetupPanel.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "SetupPanel.SingleFileComponent.g.cs");
        CompileErrors(CompileGeneratedWithHelperStub(generated)).ShouldBeEmpty();
    }

    private static SourceLocation Loc(int startLine, int startColumn, int endLine, int endColumn, string source)
        => new(new Position(0, startLine, startColumn), new Position(source.Length, endLine, endColumn), source);

    // Compiles the generated .g.cs together with a minimal runtime render-helper stub so every emitted
    // helper call (_openBlock/_createElementBlock/_toDisplayString) and the [V01.01.06.07]
    // IComponentTemplate bridge bind, leaving the unresolved template member (`_ctx.Cont`) as the SOLE
    // compile error — the one the #line map must relocate. These templates use no DOM-only capability,
    // so the generator emits no Browser helper import and the stub remains host-neutral.
    private static ImmutableArray<RoslynDiagnostic> CompileGeneratedWithHelperStub(string generated)
    {
        const string helperStub =
            "namespace Assimalign.Viu.Components\n" +
            "{\n" +
            "    internal interface IComponent { }\n" +
            "    internal interface IComponentContext { }\n" +
            "    internal delegate IComponent? ComponentRenderer();\n" +
            "    internal interface IComponentTemplate\n" +
            "    {\n" +
            "        string? Name { get; }\n" +
            "        ComponentRenderer Setup(IComponentContext context);\n" +
            "    }\n" +
            "}\n" +
            "namespace Assimalign.Viu\n" +
            "{\n" +
            "    internal static class RenderHelpers\n" +
            "    {\n" +
            "        internal static object _openBlock(bool disableTracking = false) => null!;\n" +
            "        internal static object? _createElementBlock(object token, object tag, object? props = null, object? children = null, int patchFlag = 0, string[]? dynamicProps = null) => null;\n" +
            "        internal static string _toDisplayString(object? value) => \"\";\n" +
            "        internal static Components.IComponent NormalizeRoot(object? value) => null!;\n" +
            "    }\n" +
            "}\n";

        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var trees = new[] { generated, helperStub }
            .Select(text => CSharpSyntaxTree.ParseText(text, parseOptions));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        return CSharpCompilation.Create(
            "Assimalign.Viu.Generators.Syntax.TemplateSourceMapTestAssembly",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .GetDiagnostics();
    }

    private static System.Collections.Generic.IReadOnlyList<RoslynDiagnostic> CompileErrors(
        ImmutableArray<RoslynDiagnostic> diagnostics)
        => diagnostics.Where(diagnostic => diagnostic.Severity == RoslynDiagnosticSeverity.Error).ToList();
}
