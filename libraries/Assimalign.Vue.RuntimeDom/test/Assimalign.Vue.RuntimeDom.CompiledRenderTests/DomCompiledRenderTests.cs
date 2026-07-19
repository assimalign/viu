using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using Shouldly;
using Xunit;

using Assimalign.Vue.RuntimeCore;
using Assimalign.Vue.RuntimeDom;
using Assimalign.Vue.Syntax.Generators;

namespace Assimalign.Vue.RuntimeDom.CompiledRenderTests;

/// <summary>
/// The [V01.01.04.09] compile-binding proof for the DOM half of the render-helper contract — the DOM
/// analogue of <c>Assimalign.Vue.RuntimeCore.CompiledRenderTests</c>. A real <c>.viu</c> that uses every DOM
/// directive spelling — <c>v-show</c>, a <c>v-model</c> on each input kind (text, checkbox, radio, select,
/// dynamic <c>:type</c>), and modifier/key event handlers — is compiled by
/// <see cref="SingleFileComponentGenerator"/>, and the generated render body is semantically compiled with
/// <c>Microsoft.CodeAnalysis</c> against BOTH helper surfaces: <see cref="RenderHelpers"/> (runtime-core) and
/// <see cref="DomRenderHelpers"/> (RuntimeDom). This proves the generator's second <c>using static</c> plus
/// the DOM facade close the loop the runtime-core surface deliberately left open ([V01.01.03.22]): a
/// DOM-directive template is now end-to-end compilable. The generators/compiler appear ONLY in this test
/// project, never in runtime code; the generated render is pure runtime C#.
/// <para>
/// Execution coverage (v-show display toggling, a <c>.prevent</c> handler) is proved separately through the
/// in-memory DOM adapter in <c>Assimalign.Vue.RuntimeDom.Tests.DomRenderHelpersTests</c>, because that
/// adapter (int node handles, <c>BrowserDirectiveOperations</c>, the event invoker registry) is the one the
/// existing v-show/v-model runtime tests use.
/// </para>
/// </summary>
public sealed class DomCompiledRenderTests
{
    // Exercises every DOM directive spelling the emitter can produce, matching the pinned
    // vuejs/core compiler-dom vModel element/type -> directive mapping (VModelTransformTests):
    // v-show, vModelText (input), vModelCheckbox, vModelRadio, vModelSelect, vModelDynamic (:type),
    // withModifiers (@click.prevent inline), and withKeys nested over withModifiers (@keydown.enter.stop).
    private const string DomDirectiveTemplate =
        "@template {\n" +
        "<div v-show=\"visible\">\n" +
        "  <input v-model=\"textModel\" />\n" +
        "  <input type=\"checkbox\" v-model=\"checkModel\" />\n" +
        "  <input type=\"radio\" value=\"a\" v-model=\"radioModel\" />\n" +
        "  <select v-model=\"selectModel\"><option value=\"x\">X</option></select>\n" +
        "  <input :type=\"kind\" v-model=\"dynamicModel\" />\n" +
        "  <button @click.prevent=\"clicks++\">go</button>\n" +
        "  <input @keyup.enter=\"onEnter\" />\n" +
        "  <input @keydown.enter.stop=\"onEscape\" />\n" +
        "</div>\n" +
        "}\n";

    // The hand-written half of the partial class the generated render binds against. v-model models are
    // object? because the emitted `onUpdate:modelValue` handler assigns the object?-typed __event back to
    // them; the event handlers take a BrowserEvent (the .prevent/key guard argument).
    private const string HandWrittenHalf =
        "#nullable enable\n" +
        "using Assimalign.Vue.RuntimeDom;\n" +
        "namespace Demo\n" +
        "{\n" +
        "    partial class DomWidget\n" +
        "    {\n" +
        "        public bool visible => true;\n" +
        "        public object? textModel { get; set; }\n" +
        "        public object? checkModel { get; set; }\n" +
        "        public object? radioModel { get; set; }\n" +
        "        public object? selectModel { get; set; }\n" +
        "        public object? dynamicModel { get; set; }\n" +
        "        public string kind => \"text\";\n" +
        "        public int clicks;\n" +
        "        public void onEnter(BrowserEvent browserEvent) { }\n" +
        "        public void onEscape(BrowserEvent browserEvent) { }\n" +
        "    }\n" +
        "}\n";

    [Fact]
    public void DomDirectiveTemplate_EmitsBothHelperImports_AndEveryDomHelperSpelling()
    {
        // The generator emits both file-level using-static imports so the render body binds by name against
        // the runtime-core AND the DOM helper surfaces, and the DOM directive/modifier spellings the emitter
        // writes are present verbatim.
        var generated = CompiledRenderSupport.Generate("DomWidget", DomDirectiveTemplate);

        generated.ShouldContain("using static global::Assimalign.Vue.RuntimeCore.RenderHelpers;");
        generated.ShouldContain("using static global::Assimalign.Vue.RuntimeDom.DomRenderHelpers;");

        generated.ShouldContain("_vShow");
        generated.ShouldContain("_vModelText");
        generated.ShouldContain("_vModelCheckbox");
        generated.ShouldContain("_vModelRadio");
        generated.ShouldContain("_vModelSelect");
        generated.ShouldContain("_vModelDynamic");
        generated.ShouldContain("_withModifiers");
        generated.ShouldContain("_withKeys");
    }

    [Fact]
    public void DomDirectiveTemplate_CompilesAgainstBothFacades()
    {
        // The by-name contract's compile-time proof: the generated render body binds every DOM helper
        // (directive values, modifier/key guards) against DomRenderHelpers and every runtime-core helper
        // against RenderHelpers, together with the hand-written component half. CompileToAssembly throws with
        // the full diagnostics on any binding gap.
        var generated = CompiledRenderSupport.Generate("DomWidget", DomDirectiveTemplate);
        var assembly = CompiledRenderSupport.CompileToAssembly(generated, HandWrittenHalf);
        assembly.Length.ShouldBeGreaterThan(0);
    }

    // A component that exercises both [V01.01.06.06] seams: a `module` block (the typed accessor) and a
    // `v-bind()` block (the ApplyCssVariables call into UseCssVars). The hand-written half supplies the
    // members the emitted v-bind getter evaluates.
    private const string CssModuleAndVBindComponent =
        "@template {\n" +
        "<div class=\"box\">hi</div>\n" +
        "}\n" +
        "@style module {\n" +
        ".box { color: v-bind(color); width: v-bind(size); }\n" +
        "}\n";

    private const string CssModuleHandWrittenHalf =
        "#nullable enable\n" +
        "namespace Demo\n" +
        "{\n" +
        "    partial class CssWidget\n" +
        "    {\n" +
        "        public string color => \"red\";\n" +
        "        public int size => 10;\n" +
        "    }\n" +
        "}\n";

    [Fact]
    public void CssModuleAndVBind_SeamsCompile_AgainstRuntimeDom()
    {
        // The generated $style accessor and the ApplyCssVariables -> UseCssVars seam are real runtime C#:
        // the accessor's const members compile, and the getter binds CssVariables.UseCssVars in RuntimeDom
        // while its expressions resolve against the merged component members. This proves the [V01.01.06.06]
        // metadata the runtime half consumes is well-formed, DOM-free.
        var generated = CompiledRenderSupport.Generate("CssWidget", CssModuleAndVBindComponent);

        generated.ShouldContain("internal static class Style");
        generated.ShouldContain("internal void ApplyCssVariables()");
        generated.ShouldContain("global::Assimalign.Vue.RuntimeDom.CssVariables.UseCssVars(");

        var assembly = CompiledRenderSupport.CompileToAssembly(generated, CssModuleHandWrittenHalf);
        assembly.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GeneratorCaching_ForTheDomDirectiveTemplate_StaysStrictlyCached()
    {
        // The DOM using-static is part of the fixed preamble, downstream of the value-equatable model, so it
        // does not perturb incrementality: identical input re-runs to an equal model, leaving the model step
        // strictly Cached (not Unchanged, which would mean it re-executed and merely matched).
        var file = new InMemoryAdditionalText("C:/proj/DomWidget.viu", DomDirectiveTemplate);
        var compilation = CSharpCompilation.Create(
            "GeneratorInput",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CompiledRenderSupport.CreateDriver(file);

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);

        driver.GetRunResult().Results[0]
            .TrackedSteps[SingleFileComponentGenerator.ModelTrackingName]
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void VoidInlineHandlerWithModifier_CompilesAndExecutes_ThroughWithModifiersFacade()
    {
        // [V01.01.05.05.01] A void single-statement inline handler WITH a modifier emits as a statement-block
        // lambda wrapped by the DOM guard: `_withModifiers(__event => { _ctx.record(__event); }, ["prevent"])`,
        // binding DomRenderHelpers._withModifiers(Action<BrowserEvent>, params string[]). Before the fix the
        // emitter wrote the parenthesized void call `__event => (_ctx.record(__event))`, which binds neither the
        // Func<BrowserEvent, object?> nor the Action<BrowserEvent> overload — the generated render failed to
        // compile. Here the generated render compiles AND, when the stored guarded onClick delegate is invoked,
        // runs the void method and applies .prevent to the event (upstream compiler-dom transformOn's
        // withModifiers wrapping: vuejs/core v3.5).
        const string template = "@template {\n<button @click.prevent=\"record($event)\">go</button>\n}\n";
        const string handWritten =
            "#nullable enable\n" +
            "using Assimalign.Vue.RuntimeDom;\n" +
            "namespace Demo\n" +
            "{\n" +
            "    partial class VoidModifierHandler\n" +
            "    {\n" +
            "        public int RecordCount;\n" +
            "        public BrowserEvent? LastEvent;\n" +
            "        public void record(BrowserEvent browserEvent) { RecordCount++; LastEvent = browserEvent; }\n" +
            "    }\n" +
            "}\n";

        var generated = CompiledRenderSupport.Generate("VoidModifierHandler", template);
        // The void call emits as a statement-block lambda wrapped by the withModifiers guard.
        generated.ShouldContain("_withModifiers(__event => { _ctx.record(__event); }, [\"prevent\"])");

        var type = Assembly.Load(CompiledRenderSupport.CompileToAssembly(generated, handWritten))
            .GetType("Demo.VoidModifierHandler")
            ?? throw new InvalidOperationException("The compiled assembly did not contain Demo.VoidModifierHandler.");
        var instance = Activator.CreateInstance(type, nonPublic: true)!;
        var cacheSize = (int)type.GetField("RenderCacheSize", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetRawConstantValue()!;
        var render = type.GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Static)!;

        var vnode = (VirtualNode)render.Invoke(null, new object?[] { instance, new object?[cacheSize] })!;
        var handler = vnode.Properties!["onClick"].ShouldBeOfType<Action<BrowserEvent>>();

        // BrowserEvent's constructor is internal and this compile-binding project has no InternalsVisibleTo
        // grant, so the dispatch event is built through the internal constructor by reflection (the argument
        // order matches Assimalign.Vue.RuntimeDom.Events.BrowserEvent).
        var browserEvent = (BrowserEvent)Activator.CreateInstance(
            typeof(BrowserEvent),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: new object?[] { "click", 0.0, "", "", BrowserEventModifiers.None, -1, 0, 0.0, 0.0, 0, true, null, false, null },
            culture: null)!;
        handler(browserEvent);

        type.GetField("RecordCount")!.GetValue(instance).ShouldBe(1);            // the void handler ran once
        type.GetField("LastEvent")!.GetValue(instance).ShouldBeSameAs(browserEvent);
        browserEvent.DefaultPrevented.ShouldBeTrue();                            // .prevent applied via the guard
    }
}

/// <summary>
/// Codegen-plus-Roslyn harness: runs the real source generator over a <c>.viu</c> template and compiles the
/// generated render body against both the runtime-core and DOM helper surfaces. The DOM analogue of the
/// runtime-core <c>CompiledRenderSupport</c>; the compiler/parser assemblies are kept out of the generated
/// code's reference set so the generated render is pure runtime C#.
/// </summary>
internal static class CompiledRenderSupport
{
    private const string RootNamespace = "Demo";
    private const string ProjectDirectory = "C:/proj";

    /// <summary>Runs the generator over one <c>@template</c> and returns the generated partial-class source.</summary>
    internal static string Generate(string componentName, string template)
    {
        var file = new InMemoryAdditionalText($"{ProjectDirectory}/{componentName}.viu", template);
        var compilation = CSharpCompilation.Create(
            "GeneratorInput",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var result = CreateDriver(file).RunGenerators(compilation).GetRunResult().Results[0];

        result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();
        return result.GeneratedSources
            .Single(source => source.HintName.EndsWith($"{componentName}.SingleFileComponent.g.cs", StringComparison.Ordinal))
            .SourceText.ToString();
    }

    /// <summary>Creates a step-tracking generator driver over one in-memory <c>.viu</c> file.</summary>
    internal static GeneratorDriver CreateDriver(AdditionalText file)
    {
        var globalOptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.RootNamespace"] = RootNamespace,
            ["build_property.ProjectDir"] = ProjectDirectory,
        };
        return CSharpGeneratorDriver.Create(
            generators: new[] { new SingleFileComponentGenerator().AsSourceGenerator() },
            additionalTexts: ImmutableArray.Create(file),
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            optionsProvider: new InMemoryOptionsProvider(globalOptions),
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    }

    /// <summary>
    /// Semantically compiles the generated render body together with a hand-written partial-class half
    /// against both helper surfaces, returning the emitted assembly bytes. Throws with the full diagnostics
    /// when binding fails — the DOM by-name helper contract's compile-time proof.
    /// </summary>
    internal static byte[] CompileToAssembly(string generatedSource, string handWrittenHalf)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(generatedSource, parseOptions),
            CSharpSyntaxTree.ParseText(handWrittenHalf, parseOptions),
        };

        var compilation = CSharpCompilation.Create(
            "Demo.Compiled." + Guid.NewGuid().ToString("N"),
            trees,
            ResolveReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        if (!emit.Success)
        {
            var errors = string.Join(
                Environment.NewLine,
                emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException(
                "The generated DOM-directive render body failed to compile against the helper surfaces:" +
                Environment.NewLine + errors + Environment.NewLine + Environment.NewLine + generatedSource);
        }
        return stream.ToArray();
    }

    private static IReadOnlyList<MetadataReference> ResolveReferences()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();
        void Add(string? location)
        {
            if (!string.IsNullOrEmpty(location) && seen.Add(location!))
            {
                references.Add(MetadataReference.CreateFromFile(location!));
            }
        }

        // The framework's trusted platform assemblies plus exactly the Vuecs assemblies the generated render
        // and its hand-written half bind against — both helper surfaces (RuntimeCore + RuntimeDom), and Shared
        // looked up by name so no linked shared type is referenced here. The compiler/parser assemblies are
        // deliberately NOT referenced: the generated render is pure runtime C#.
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
        {
            foreach (var path in trusted.Split(Path.PathSeparator))
            {
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    Add(path);
                }
            }
        }
        Add(typeof(object).Assembly.Location);
        Add(typeof(RenderHelpers).Assembly.Location);
        Add(typeof(DomRenderHelpers).Assembly.Location);
        var shared = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Assimalign.Vue.Shared", StringComparison.Ordinal));
        Add(shared?.Location);
        return references;
    }
}

/// <summary>An in-memory <c>.viu</c> additional file for the source generator.</summary>
internal sealed class InMemoryAdditionalText : AdditionalText
{
    private readonly SourceText _text;

    internal InMemoryAdditionalText(string path, string content)
    {
        Path = path;
        _text = SourceText.From(content);
    }

    public override string Path { get; }

    public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
}

/// <summary>An <see cref="AnalyzerConfigOptions"/> over a fixed global build-property map.</summary>
internal sealed class InMemoryOptions : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> _values;

    internal InMemoryOptions(Dictionary<string, string> values) => _values = values;

    public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
}

/// <summary>An <see cref="AnalyzerConfigOptionsProvider"/> returning one global options set everywhere.</summary>
internal sealed class InMemoryOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions _options;

    internal InMemoryOptionsProvider(Dictionary<string, string> globalOptions) => _options = new InMemoryOptions(globalOptions);

    public override AnalyzerConfigOptions GlobalOptions => _options;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
}
