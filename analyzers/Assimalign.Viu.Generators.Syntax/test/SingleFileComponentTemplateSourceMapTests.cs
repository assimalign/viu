using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.Templates;
using Assimalign.Viu.Compiler.SingleFileComponent;

using Shouldly;
using Xunit;

// Both the Viu syntax cluster and Roslyn expose Diagnostic and DiagnosticSeverity; alias the Roslyn
// types that the generated code compiles against.
using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Tests for [V01.01.05.08] — the render-body source map: the compiled template render function is
/// wrapped in <c>#line</c> span directives so a C# compile error inside an emitted template expression
/// resolves to the offending <c>.viu</c> template line and column, never to opaque generated code. This is
/// the render-body analogue of the <c>@script</c> merge's <c>#line</c> map (see
/// <see cref="SingleFileComponentScriptTests"/>), and it is proved the same way: through the real C#
/// compiler, asserting <c>GetMappedLineSpan()</c>. Viu carries the mapping on Roslyn <c>#line</c>
/// directives rather than a side-car source map because every Viu diagnostic already travels through the
/// C# compiler — one mechanism the compiler, the debugger, and the editor all honor without a second
/// artifact to keep in sync.
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
            "<template>\n" +                // line 1
            "<div>{{ Cont }}</div>\n" +     // line 2: `Cont` begins at column 9 (zero-based char 8)
            "</template>\n";                // line 3

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
            "<template>\n" +
            "        <span>{{ Missing }}</span>\n" +  // `Missing` begins at column 18 (zero-based char 17)
            "</template>\n";

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
            "<template>\n" +
            "<p>{{ label }}</p>\n" +
            "</template>\n";

        var generated = GeneratorTestHarness.GeneratedSource(
            GeneratorTestHarness.Run($"{ProjectDirectory}/Tag.viu", source, RootNamespace, ProjectDirectory),
            "Tag.SingleFileComponent.g.cs");

        // `label` is at file line 2, column 7 (one-based) through column 12 (its 5-char span end).
        generated.ShouldContain("#line (2,7)-(2,12) ");
        generated.ShouldContain("\"C:/proj/Tag.viu\"");
        generated.ShouldContain(
            "global::Assimalign.Viu.DisplayStringFormatter.ToDisplayString(component.label)");
        generated.ShouldContain("#line default");
    }

    [Fact]
    public void StaticOnlyTemplate_EmitsNoLineDirectives()
    {
        // A template with no dynamic expressions has nothing to map, so the render body is emitted with no
        // #line directives at all (the render source map is empty).
        const string source =
            "<template>\n" +
            "<div>static text</div>\n" +
            "</template>\n";

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
            "<template>\n" +
            "<div :id=\"dynamicId\">{{ message }}</div>\n" +
            "</template>\n";

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
        var blockStart = new Position(Offset: 12, Line: 2, Column: 1); // template block content starts on file line 2
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
            "<template>\n" +
            "    <div>ready</div>\n" +
            "</template>\n" +
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

    // Compiles the generated .g.cs together with a minimal adopted-runtime stub so the closed node
    // algebra, frame API, generated lifecycle glue, and [SFC-CG-4] IComponent bridge bind, leaving the
    // unresolved template member (`component.Cont`) as the SOLE
    // compile error — the one the #line map must relocate. These templates use no DOM-only capability,
    // so the generator emits no Browser helper import and the stub remains host-neutral.
    private static ImmutableArray<RoslynDiagnostic> CompileGeneratedWithHelperStub(string generated)
    {
        const string helperStub =
            """
            namespace Assimalign.Viu.Components
            {
                internal abstract class VirtualNode { }
                internal sealed class TextNode : VirtualNode
                {
                    internal TextNode(string? text, RenderPlan? renderPlan = null) { }
                }
                internal sealed class ElementNode : VirtualNode
                {
                    internal ElementNode(
                        QualifiedName name,
                        object? bindings = null,
                        object? children = null,
                        object? directives = null,
                        object? key = null,
                        MountReference? mountReference = null,
                        RenderPlan? renderPlan = null) { }
                }
                internal sealed class QualifiedName
                {
                    internal QualifiedName(string name) { }
                }
                internal abstract class MountReference { }
                internal sealed class ElementBinding
                {
                    internal static ElementBinding Event(string name, global::System.Delegate listener) => new();
                    internal static ElementBinding Property(string name, object? value) => new();
                    internal static ElementBinding Attribute(QualifiedName name, object? value) => new();
                }
                internal enum PatchFlags { }
                internal sealed class RenderPlan
                {
                    internal RenderPlan(PatchFlags flags, object? dynamicProperties = null, object? dynamicChildren = null) { }
                }
                internal sealed class ComponentRenderFrame
                {
                    internal object?[] Cache { get; } = [];
                    internal void OpenBlock() { }
                    internal object? CloseBlock() => null;
                    internal void Track(VirtualNode? node) { }
                    internal T GetOrAddCache<T>(int index, global::System.Func<T> factory) => factory();
                    internal T CacheHandler<T>(int index, T handler) => handler;
                }
                internal sealed class ComponentBindings
                {
                    internal global::System.Collections.Generic.IReadOnlyDictionary<string, object?> Parameters { get; } =
                        new global::System.Collections.Generic.Dictionary<string, object?>();
                    internal global::System.Collections.Generic.IReadOnlyDictionary<string, ComponentSlot> Slots { get; } =
                        new global::System.Collections.Generic.Dictionary<string, ComponentSlot>();
                }
                internal delegate VirtualNode? ComponentSlot(
                    global::System.Collections.Generic.IReadOnlyDictionary<string, object?> arguments);
                internal sealed class ComponentContext
                {
                    internal ComponentBindings Bindings { get; } = new();
                    internal global::System.IServiceProvider Services { get; } = null!;
                    internal void Emit(string name, global::System.Collections.Generic.IReadOnlyList<object?> arguments) { }
                }
                internal abstract class ComponentBase
                {
                    protected ComponentContext? Context { get; set; }
                }
                internal delegate VirtualNode? ComponentRenderer(ComponentRenderFrame frame);
                internal interface IComponent
                {
                    ComponentRenderer Setup(ComponentContext context);
                }
                internal enum ComponentFlags { InheritFallthroughBindings }
                internal sealed class ComponentContract
                {
                    internal ComponentContract(
                        string? displayName = null,
                        ComponentFlags flags = default,
                        object? parameters = null,
                        object? events = null,
                        int renderCacheSize = 0) { }
                }
                internal sealed class ComponentReference
                {
                    internal static ComponentReference ForName(string name) => new();
                }
                internal sealed class ComponentRegistration
                {
                    internal ComponentRegistration(
                        ComponentReference reference,
                        ComponentContract contract,
                        global::System.Func<global::System.IServiceProvider, IComponent> activator) { }
                }
            }
            namespace Assimalign.Viu
            {
                internal static class DisplayStringFormatter
                {
                    internal static string ToDisplayString(object? value) => string.Empty;
                }
            }
            namespace Assimalign.Viu.Generated
            {
                internal static class RenderGlue
                {
                    internal static void OnBeforeMount<T>(Components.ComponentContext context, T callback) { }
                    internal static void OnMounted<T>(Components.ComponentContext context, T callback) { }
                    internal static void OnBeforeUpdate<T>(Components.ComponentContext context, T callback) { }
                    internal static void OnUpdated<T>(Components.ComponentContext context, T callback) { }
                    internal static void OnBeforeUnmount<T>(Components.ComponentContext context, T callback) { }
                    internal static void OnUnmounted<T>(Components.ComponentContext context, T callback) { }
                    internal static void OnActivated<T>(Components.ComponentContext context, T callback) { }
                    internal static void OnDeactivated<T>(Components.ComponentContext context, T callback) { }
                    internal static void OnServerPrefetch<T>(Components.ComponentContext context, T callback) { }
                    internal static void OnErrorCaptured<T>(Components.ComponentContext context, T callback) { }
                }
            }
            """;

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
