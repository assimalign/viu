using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Assimalign.Vue.Syntax.SingleFileComponent;
using Assimalign.Vue.Syntax.Templates;

using Shouldly;
using Xunit;

// The test namespace is nested under Assimalign.Vue.Syntax, so the base cluster's Diagnostic and
// DiagnosticSeverity shadow Roslyn's; alias the Roslyn types the generated code and consumer compile
// against.
using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Vue.Syntax.Generators.Tests;

/// <summary>
/// Tests for [V01.01.06.03] — merging the <c>@script</c> block's C# into the generated partial class:
/// the <c>#line</c> mapping back to the <c>.viu</c> source, the partial-class merge with a user-authored
/// sibling <c>.cs</c>, recoverable script validation, and the binding-metadata classification the template
/// compiler consumes for ref-unwrapping. Mirrors <c>@vue/compiler-sfc</c>'s <c>compileScript()</c>
/// (https://vuejs.org/api/sfc-script-setup.html) adapted to C# partial classes.
/// </summary>
public sealed class SingleFileComponentScriptTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";

    [Fact]
    public void MergedScript_WrapsMultiLineBody_InOneLineMapAnchoredAtContentStart()
    {
        // A single #line directive at the content-start line covers the whole block: subsequent lines
        // auto-increment, so every script line maps back to its own .viu line. The @script content here
        // begins on file line 6, and #line default restores the generated file's mapping afterward.
        const string source =
            "@template {\n" +          // line 1
            "    <div></div>\n" +       // line 2
            "}\n" +                     // line 3
            "\n" +                      // line 4
            "@script {\n" +             // line 5
            "    public int First = 1;\n" +   // line 6
            "    public int Second = 2;\n" +  // line 7
            "}\n";                      // line 8

        var generated = GeneratorTestHarness.GeneratedSource(
            GeneratorTestHarness.Run($"{ProjectDirectory}/Counter.viu", source, RootNamespace, ProjectDirectory),
            "Counter.SingleFileComponent.g.cs");

        generated.ShouldContain("#line 6 \"C:/proj/Counter.viu\"\n    public int First = 1;\n    public int Second = 2;\n#line default");
    }

    [Fact]
    public void MergedPartialClass_CompilesCleanly_WithUserAuthoredSiblingPartial()
    {
        // The generated partial class carries the @script members; a user-authored sibling partial adds
        // its own members to the SAME class. Both must merge with no duplicate-member or modifier
        // conflicts from the scaffold — the scaffold deliberately emits no members of its own.
        const string source =
            "@script {\n" +
            "    public string Message = \"Hello\";\n" +
            "    public int Double(int value) => value * 2;\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Counter.viu", source, RootNamespace, ProjectDirectory);
        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Counter.SingleFileComponent.g.cs");

        const string sibling =
            "namespace Demo\n" +
            "{\n" +
            "    partial class Counter\n" +
            "    {\n" +
            "        public int Triple(int value) => value * 3 + Message.Length - Message.Length;\n" +
            "    }\n" +
            "}\n";

        CompileErrors(CompileGenerated(generated, sibling)).ShouldBeEmpty();
    }

    [Fact]
    public void ScriptTypeError_IsReportedOnTheViuFile_ViaLineDirective()
    {
        // A type error inside the script is a SEMANTIC error the generator's parse never sees, so it
        // surfaces only when the generated C# is compiled. The emitted #line map must resolve it to the
        // .viu file at the script member's own line (file line 2, zero-based line 1), never the .g.cs.
        const string source =
            "@script {\n" +                       // line 1
            "    public int Bad = \"not an int\";\n" + // line 2
            "}\n";                                // line 3

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Widget.viu", source, RootNamespace, ProjectDirectory);
        outcome.Diagnostics.ShouldBeEmpty(); // syntactically valid C#, so the generator itself reports nothing
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Widget.SingleFileComponent.g.cs");

        var conversion = CompileErrors(CompileGenerated(generated)).ShouldHaveSingleItem();
        conversion.Id.ShouldBe("CS0029"); // cannot implicitly convert 'string' to 'int'
        // GetMappedLineSpan honors the emitted #line directive (GetLineSpan would give the physical .g.cs
        // location); the mapped span is what an IDE/CLI build and the debugger resolve to.
        var span = conversion.Location.GetMappedLineSpan();
        span.Path.ShouldEndWith("Widget.viu");
        span.StartLinePosition.Line.ShouldBe(1);       // .viu file line 2, zero-based
        span.StartLinePosition.Character.ShouldBe(21); // the "not an int" literal column, preserved because
                                                       // the script is emitted flush at column 0 (no re-indent)
    }

    [Fact]
    public void MalformedScript_SurfacesRecoverableScriptDiagnostic_AtViuCoordinates()
    {
        // A syntactically broken member is recoverable: the generator still emits the scaffold and
        // surfaces a VUECS1201 script error mapped onto the .viu file, at the exact line/column of the
        // offending token - the same block-to-file composition the @template path uses.
        const string source =
            "@script {\n" +                 // line 1
            "    public int Broken = ;\n" +  // line 2  (the ';' at column 25 / index 24 is the invalid term)
            "}\n";                           // line 3

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Widget.viu", source, RootNamespace, ProjectDirectory);

        outcome.Sources.ShouldNotBeEmpty(); // recoverable — scaffold still produced
        var diagnostic = outcome.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VUECS1201");
        diagnostic.Severity.ShouldBe(RoslynDiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("C# script code CS1525"); // invalid expression term ';'
        var span = diagnostic.Location.GetLineSpan();
        span.Path.ShouldBe($"{ProjectDirectory}/Widget.viu");
        span.StartLinePosition.Line.ShouldBe(1);       // .viu file line 2, zero-based
        span.StartLinePosition.Character.ShouldBe(24); // the ';' column, composed onto the .viu line
    }

    [Fact]
    public void Classification_MapsEachMemberShape_ToItsBindingType()
    {
        // Pins the conservative, syntactic classification table (the C# port of Vue's BindingTypes):
        // only a field/property whose declared type is a known Assimalign.Vue.Reactivity reference type
        // is SetupReference (the only binding the template ever unwraps through .Value); const is a folded
        // LiteralConstant; a mutable binding is SetupLet; a fixed (readonly / get-only) binding is
        // SetupConstant; and a method is a non-ref SetupConstant. See
        // https://vuejs.org/guide/essentials/reactivity-fundamentals.html#ref-unwrapping-in-templates.
        const string content =
            "    public Reference<int> Count = default!;\n" +
            "    public ShallowReference<int> Shallow = default!;\n" +
            "    public CustomReference<int> Custom = default!;\n" +
            "    public IReference<int> Interface = default!;\n" +
            "    public Computed<string> Label { get; } = default!;\n" +
            "    public global::Assimalign.Vue.Reactivity.Reference<int> Qualified = default!;\n" +
            "    public const int Max = 10;\n" +
            "    public readonly string Name = \"x\";\n" +
            "    public int Mutable = 0;\n" +
            "    public string Title { get; set; } = \"\";\n" +
            "    public string Caption { get; } = \"\";\n" +
            "    public int Compute() => 1;\n";

        var diagnostics = new List<DiagnosticInfo>();
        var bindings = ScriptBlockAnalyzer.Analyze("C:/proj/Counter.viu", ScriptBlock(content), diagnostics);

        diagnostics.ShouldBeEmpty(); // well-formed C# produces no script diagnostics
        var map = bindings.ToDictionary(binding => binding.Name, binding => binding.Type);

        map["Count"].ShouldBe(BindingType.SetupReference);
        map["Shallow"].ShouldBe(BindingType.SetupReference);
        map["Custom"].ShouldBe(BindingType.SetupReference);
        map["Interface"].ShouldBe(BindingType.SetupReference);
        map["Label"].ShouldBe(BindingType.SetupReference);
        map["Qualified"].ShouldBe(BindingType.SetupReference);
        map["Max"].ShouldBe(BindingType.LiteralConstant);
        map["Name"].ShouldBe(BindingType.SetupConstant);
        map["Mutable"].ShouldBe(BindingType.SetupLet);
        map["Title"].ShouldBe(BindingType.SetupLet);
        map["Caption"].ShouldBe(BindingType.SetupConstant);
        map["Compute"].ShouldBe(BindingType.SetupConstant);
    }

    [Fact]
    public void UsingDirectiveInScript_IsReportedRecoverably_HoistingDeferred()
    {
        // Interim contract: this iteration merges @script as a partial-class BODY (member declarations),
        // so a leading `using` — legal in Vue's <script setup> and used by the design sample — is parsed
        // in class-body context and surfaces as a recoverable script diagnostic on the .viu file rather
        // than crashing. First-class using-directive hoisting is deferred to the remaining [V01.01.06.03]
        // scope; when it lands this test is updated to assert the hoisted directive instead.
        const string source =
            "@script {\n" +
            "    using System.Text;\n" +
            "    public string Name = \"x\";\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Widget.viu", source, RootNamespace, ProjectDirectory);

        outcome.Sources.ShouldNotBeEmpty(); // recoverable — scaffold still produced
        outcome.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.Id == "VUECS1201" && diagnostic.Location.GetLineSpan().Path == $"{ProjectDirectory}/Widget.viu");
    }

    [Fact]
    public void Classification_ClassifiesEveryVariable_InAMultiDeclaratorField()
    {
        // A field can declare several variables; each is classified with the shared field type, so both
        // reference-typed names unwrap.
        var bindings = ScriptBlockAnalyzer.Analyze(
            "C:/proj/Counter.viu",
            ScriptBlock("    public Reference<int> First = default!, Second = default!;\n"),
            new List<DiagnosticInfo>());

        var map = bindings.ToDictionary(binding => binding.Name, binding => binding.Type);
        map["First"].ShouldBe(BindingType.SetupReference);
        map["Second"].ShouldBe(BindingType.SetupReference);
    }

    [Fact]
    public void ToBindingMetadata_ExposesClassifiedBindings_AsScriptSetupState()
    {
        // The render-code-generation path consumes the classification as a Templates.BindingMetadata; a
        // scripted component reports IsScriptSetup (Vue's __isScriptSetup) and resolves each member's type.
        var bindings = new EquatableArray<ScriptBinding>(new[]
        {
            new ScriptBinding("Count", BindingType.SetupReference),
            new ScriptBinding("Name", BindingType.SetupConstant),
        });
        var model = new SingleFileComponentModel(
            Namespace: "Demo",
            ClassName: "Counter",
            FileName: "Counter.viu",
            HintName: "Counter.SingleFileComponent.g.cs",
            HasTemplate: false,
            HasScript: true,
            StyleCount: 0,
            CustomBlockCount: 0,
            FilePath: "C:/proj/Counter.viu",
            ScriptContent: "    public Reference<int> Count = default!;\n",
            ScriptContentStartLine: 1,
            Bindings: bindings,
            RenderBody: null,
            RenderCacheSize: 0,
            ScopeId: null,
            ExtractedStyles: null);

        var metadata = model.ToBindingMetadata();

        metadata.IsScriptSetup.ShouldBeTrue();
        metadata.TryGetBindingType("Count", out var count).ShouldBeTrue();
        count.ShouldBe(BindingType.SetupReference);
        metadata.TryGetBindingType("Name", out var name).ShouldBeTrue();
        name.ShouldBe(BindingType.SetupConstant);
        metadata.Contains("Absent").ShouldBeFalse();
    }

    [Fact]
    public void ToBindingMetadata_ForScriptlessComponent_IsEmptyAndNotScriptSetup()
    {
        var model = new SingleFileComponentModel(
            Namespace: "Demo",
            ClassName: "Counter",
            FileName: "Counter.viu",
            HintName: "Counter.SingleFileComponent.g.cs",
            HasTemplate: true,
            HasScript: false,
            StyleCount: 0,
            CustomBlockCount: 0,
            FilePath: "C:/proj/Counter.viu",
            ScriptContent: null,
            ScriptContentStartLine: 0,
            Bindings: EquatableArray<ScriptBinding>.Empty,
            RenderBody: null,
            RenderCacheSize: 0,
            ScopeId: null,
            ExtractedStyles: null);

        var metadata = model.ToBindingMetadata();

        metadata.IsScriptSetup.ShouldBeFalse();
        metadata.Contains("Anything").ShouldBeFalse();
    }

    [Fact]
    public void ScriptWithBindings_IdenticalInput_StaysStrictlyCached()
    {
        // Adding script parsing + classification inside the model step must not break incrementality:
        // identical input re-runs to an equal model (equal ScriptContent, equal Bindings), so the step
        // is strictly Cached - not Unchanged, which would mean it re-executed and merely matched.
        const string source =
            "@script {\n" +
            "    public Reference<int> Count = default!;\n" +
            "    public string Name = \"x\";\n" +
            "    public int Add() => 1;\n" +
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

    // Builds a script block over raw content with a first-line content anchor (offset 0, line 1, column 1).
    // The exact anchor only matters for diagnostic mapping; classification tests ignore it.
    private static SingleFileComponentScriptBlock ScriptBlock(string content)
    {
        var location = new SourceLocation(new Position(0, 1, 1), new Position(content.Length, 1, 1), content);
        return new SingleFileComponentScriptBlock
        {
            Name = "script",
            Options = SyntaxList<SingleFileComponentBlockOption>.Empty,
            Content = content,
            Location = location,
            ContentLocation = location,
        };
    }

    // Compiles generated source (optionally with sibling partials) against the full framework reference
    // set so semantic diagnostics — the ones the emitted #line map must relocate — actually surface.
    private static ImmutableArray<RoslynDiagnostic> CompileGenerated(params string[] sources)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source, parseOptions));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        return CSharpCompilation.Create(
            "Assimalign.Vue.Syntax.Generators.ScriptMergeTestAssembly",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .GetDiagnostics();
    }

    private static IReadOnlyList<RoslynDiagnostic> CompileErrors(ImmutableArray<RoslynDiagnostic> diagnostics)
        => diagnostics.Where(diagnostic => diagnostic.Severity == RoslynDiagnosticSeverity.Error).ToList();

    [Fact]
    public void ScriptReference_DrivesRenderUnwrap_EndToEnd()
    {
        // The [V01.01.06.03] -> [V01.01.05.05] hand-off: the @script block declares a Reference<int>
        // member, so the @template's use of it compiles to a _ctx-routed .Value unwrap in the emitted
        // render body — the whole point of feeding script-classified BindingMetadata into the template
        // compiler (upstream analogue: SETUP_REF resolving through $setup in function mode).
        const string source =
            "@template {\n" +
            "    <div>{{ Count }}</div>\n" +
            "}\n" +
            "@script {\n" +
            "    public Assimalign.Vue.Reactivity.Reference<int> Count = new(0);\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Counter.viu", source, RootNamespace, ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Counter.SingleFileComponent.g.cs");
        generated.ShouldContain("_toDisplayString(_ctx.Count.Value)");
    }
}
