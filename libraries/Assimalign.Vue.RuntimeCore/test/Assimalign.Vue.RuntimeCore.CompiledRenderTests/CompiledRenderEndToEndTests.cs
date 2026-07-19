using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using Shouldly;
using Xunit;

using Assimalign.Vue.Reactivity;
using Assimalign.Vue.RuntimeCore;
using Assimalign.Vue.Syntax.Generators;
using Assimalign.Vue.Testing;

namespace Assimalign.Vue.RuntimeCore.CompiledRenderTests;

/// <summary>
/// The [V01.01.03.22] flagship deliverable — the deferred [V01.01.05.05] integration criterion. A real
/// <c>.viu</c> component is compiled by <see cref="SingleFileComponentGenerator"/>, the generated render
/// body is compiled with <c>Microsoft.CodeAnalysis</c> in this test project (the generators/compiler appear
/// ONLY here, never in runtime code), the compiled <c>Render</c> binds by name to
/// <see cref="RenderHelpers"/> through <c>using static</c>, and it executes against the in-memory
/// <see cref="TestRenderer"/> — asserting the rendered tree, a <c>v-if</c> toggle and keyed <c>v-for</c>
/// reorder driving patch-flag-guided updates, and a <c>v-once</c> subtree that never re-renders (run counts
/// pinned per the testing rules). This proves the by-name helper contract end to end: template → compiler →
/// generated C# → runtime helpers → renderer → observed tree.
/// <para>
/// The proof lives in its own project (nested under RuntimeCore <c>test/</c>, mirroring
/// <c>Assimalign.Vue.Shared.GeneratorFixture</c>) so the generator's transitive
/// <c>Assimalign.Vue.Syntax.Templates</c> reference — which links copies of Shared's
/// <c>PatchFlags</c>/<c>SlotFlags</c> — never collides with <c>Shared.dll</c> in the main
/// <c>RuntimeCore.Tests</c> compile.
/// </para>
/// </summary>
public sealed class CompiledRenderEndToEndTests : IClassFixture<CompiledRenderEndToEndTests.CompiledCounter>, IDisposable
{
    private readonly CompiledCounter _compiled;
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public CompiledRenderEndToEndTests(CompiledCounter compiled)
    {
        _compiled = compiled;
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void GeneratedRender_MountsTheCompiledTree_AgainstTheInMemoryRenderer()
    {
        // The end-to-end mount: the generator-emitted, Roslyn-compiled Render binds to RenderHelpers by
        // name and produces the expected block tree — a v-if <p>, a keyed v-for <ul>, and a v-once <span>.
        var counter = _compiled.CreateInstance();
        using var render = MountCounter(counter, out var renders);

        renders.Value.ShouldBe(1);
        TestNodeSerializer.Serialize(_container).ShouldBe(
            "<root><div><p>hi</p><ul><li>a</li><li>b</li></ul><span>frozen</span></div></root>");
        counter.FrozenReadCount.ShouldBe(1); // the v-once subtree read its source exactly once at mount
    }

    [Fact]
    public void VIfToggle_SwapsBranchWithoutTouchingSiblings_AndReactiveMutationDrivesIt()
    {
        // A reactive write to the v-if condition enqueues a scheduled re-render (not synchronous); the
        // flush swaps the <p> for the comment placeholder and back — the patch-flag block tree patches only
        // the v-if child.
        var counter = _compiled.CreateInstance();
        using var render = MountCounter(counter, out var renders);

        counter.Visible = false;
        renders.Value.ShouldBe(1); // not synchronous — waits for the flush
        _pump.RunUntilIdle();

        renders.Value.ShouldBe(2);
        TestNodeSerializer.Serialize(_container).ShouldBe(
            "<root><div><!--v-if--><ul><li>a</li><li>b</li></ul><span>frozen</span></div></root>");

        counter.Visible = true;
        _pump.RunUntilIdle();
        renders.Value.ShouldBe(3);
        TestNodeSerializer.Serialize(_container).ShouldBe(
            "<root><div><p>hi</p><ul><li>a</li><li>b</li></ul><span>frozen</span></div></root>");
    }

    [Fact]
    public void DynamicText_InsideVIfBranch_PatchesThroughTheTextFlag()
    {
        // The {{ message }} interpolation inside the v-if <p> carries PatchFlags.Text; a reactive write
        // re-renders and lands a single targeted text update, no structural work.
        var counter = _compiled.CreateInstance();
        using var render = MountCounter(counter, out _);
        _renderer.OperationLog.Reset();

        counter.Message = "bye";
        _pump.RunUntilIdle();

        TestNodeSerializer.Serialize(_container).ShouldBe(
            "<root><div><p>bye</p><ul><li>a</li><li>b</li></ul><span>frozen</span></div></root>");
        _renderer.OperationLog.Count(TestNodeOperationType.SetElementText).ShouldBe(1);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
    }

    [Fact]
    public void KeyedVForReorder_MovesExistingNodes_WithoutRecreatingThem()
    {
        // The keyed v-for fragment (PatchFlags.KeyedFragment) reconciles by key: reordering the items moves
        // the existing <li> elements rather than recreating them (no CreateElement), the compiler-informed
        // patch the block tree exists to enable.
        var counter = _compiled.CreateInstance();
        using var render = MountCounter(counter, out var renders);

        var listElement = FindElement(_container, "ul")!;
        var before = FindElements(listElement, "li");
        before.Select(TextOf).ShouldBe(new[] { "a", "b" });
        _renderer.OperationLog.Reset();

        counter.SetItems((2, "b"), (1, "a"));
        _pump.RunUntilIdle();

        renders.Value.ShouldBe(2);
        TestNodeSerializer.Serialize(_container).ShouldBe(
            "<root><div><p>hi</p><ul><li>b</li><li>a</li></ul><span>frozen</span></div></root>");
        // Keyed reuse: no new <li> elements were created for the reorder.
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        // The same two element instances survive, now in swapped order.
        var after = FindElements(FindElement(_container, "ul")!, "li");
        after.ShouldContain(before[0]);
        after.ShouldContain(before[1]);
    }

    [Fact]
    public void VOnceSubtree_IsRenderedOnce_AndNeverReReadsItsSourceAcrossUpdates()
    {
        // The v-once <span> is created with block tracking suspended and cached in _cache[0]; the ??= short
        // circuits on every later render, so its source getter runs exactly once no matter how many updates
        // occur, and the span node is never re-created.
        var counter = _compiled.CreateInstance();
        using var render = MountCounter(counter, out var renders);

        var spanBefore = FindElement(_container, "span");
        spanBefore.ShouldNotBeNull();
        counter.FrozenReadCount.ShouldBe(1);
        _renderer.OperationLog.Reset();

        counter.Visible = false;
        _pump.RunUntilIdle();
        counter.Visible = true;
        _pump.RunUntilIdle();
        counter.SetItems((9, "z"));
        _pump.RunUntilIdle();

        renders.Value.ShouldBe(4); // one mount + three flushed updates
        // The v-once source was read exactly once, at mount — never on any re-render.
        counter.FrozenReadCount.ShouldBe(1);
        // The cached <span> instance is reused, never recreated.
        FindElement(_container, "span").ShouldBeSameAs(spanBefore);
        _renderer.OperationLog.OfType(TestNodeOperationType.CreateElement)
            .ShouldNotContain(operation => ReferenceEquals(operation.TargetNode, spanBefore));
    }

    [Fact]
    public void ComponentTemplate_WithSlotsHandlersAndVFor_BindsAgainstTheRuntimeSurface()
    {
        // Compile-binding proof for the helpers the flagship execution flow does not itself exercise:
        // components (_createBlock via _resolveComponent), scoped + default slots (_withCtx and _createProps
        // as a slots object), an event handler (_withHandler), and v-for (_renderList) all bind by name
        // against RenderHelpers when the generated render body is SEMANTICALLY compiled — the deferred
        // integration criterion for these helpers, over the Templates project's parse-only pinning.
        const string template =
            "@template {\n" +
            "<MyButton :kind=\"kind\" @press=\"onPress\">" +
            "<template #header=\"header\">{{ header }}</template>" +
            "<li v-for=\"item in items\" :key=\"item.id\">{{ item.label }}</li>" +
            "</MyButton>\n" +
            "}\n";
        const string handWritten =
            "#nullable enable\n" +
            "using System.Collections.Generic;\n" +
            "namespace Demo\n" +
            "{\n" +
            "    public sealed class PanelItem\n" +
            "    {\n" +
            "        public PanelItem(int id, string label) { this.id = id; this.label = label; }\n" +
            "        public int id { get; }\n" +
            "        public string label { get; }\n" +
            "    }\n" +
            "    partial class Panel\n" +
            "    {\n" +
            "        public string kind => \"primary\";\n" +
            "        public void onPress() { }\n" +
            "        public IReadOnlyList<PanelItem> items => new List<PanelItem>();\n" +
            "    }\n" +
            "}\n";

        var generated = CompiledRenderSupport.Generate("Panel", template);
        // The emitted body exercises the component / slot / handler / list helper shapes.
        generated.ShouldContain("_resolveComponent(\"MyButton\")");
        generated.ShouldContain("_withCtx(");
        generated.ShouldContain("_renderList(_ctx.items,");
        generated.ShouldContain("_withHandler(_ctx.onPress)");

        // Semantic compile against the runtime helper surface — throws with diagnostics on any binding gap.
        var assembly = CompiledRenderSupport.CompileToAssembly(generated, handWritten);
        assembly.Length.ShouldBeGreaterThan(0);
    }

    // Wires the compiled component's static Render into a root render effect: the closure runs the generated
    // Render (reactive reads inside it are tracked) and normalizes the object? result to a vnode.
    private RenderEffect<TestNode> MountCounter(IRenderHarness counter, out StrongBox<int> renders)
    {
        var cache = new object?[counter.CacheSize];
        var renderCount = new StrongBox<int>(0);
        renders = renderCount;
        return _renderer.Renderer.CreateRenderEffect(
            () =>
            {
                renderCount.Value++;
                return RenderHelpers.NormalizeRoot(counter.Invoke(cache));
            },
            _container);
    }

    private static string TextOf(TestElement element)
        => element.Children.OfType<TestText>().Aggregate(string.Empty, (text, node) => text + node.Text);

    private static TestElement? FindElement(TestNode node, string tag)
    {
        if (node is TestElement element)
        {
            if (string.Equals(element.Tag, tag, StringComparison.Ordinal))
            {
                return element;
            }
            foreach (var child in element.Children)
            {
                var found = FindElement(child, tag);
                if (found is not null)
                {
                    return found;
                }
            }
        }
        return null;
    }

    private static List<TestElement> FindElements(TestElement parent, string tag)
    {
        var result = new List<TestElement>();
        foreach (var child in parent.Children)
        {
            if (child is TestElement element && string.Equals(element.Tag, tag, StringComparison.Ordinal))
            {
                result.Add(element);
            }
        }
        return result;
    }

    /// <summary>
    /// A class fixture that compiles the <c>.viu</c> component once: it runs the real source generator over
    /// the <c>@template</c>, compiles the generated partial class together with a hand-written reactive-state
    /// half using Roslyn, and loads the resulting assembly. The two halves form one <c>partial class</c>, so
    /// the generated <c>Render</c> binds against the hand-written members exactly as a real component would.
    /// </summary>
    public sealed class CompiledCounter
    {
        private const string RootNamespace = "Demo";
        private const string ProjectDirectory = "C:/proj";

        // The .viu template exercises every acceptance-criteria feature: a v-if branch with dynamic text, a
        // keyed v-for, and a v-once subtree. Template identifiers bind case-sensitively to the hand-written
        // members (_ctx.visible, _ctx.message, _ctx.items, item.id, item.label, _ctx.frozen).
        private const string Template =
            "@template {\n" +
            "<div>" +
            "<p v-if=\"visible\">{{ message }}</p>" +
            "<ul><li v-for=\"item in items\" :key=\"item.id\">{{ item.label }}</li></ul>" +
            "<span v-once>{{ frozen }}</span>" +
            "</div>\n" +
            "}\n";

        // The hand-written half of the partial class: reactive-backed template bindings plus the harness
        // controls. Refs make _ctx.visible/message/items tracked reads so a write drives a re-render; the
        // frozen getter counts its reads so the v-once run count can be pinned.
        private const string HandWrittenHalf =
            "#nullable enable\n" +
            "using System.Collections.Generic;\n" +
            "using Assimalign.Vue.Reactivity;\n" +
            "using Assimalign.Vue.RuntimeCore.CompiledRenderTests;\n" +
            "namespace Demo\n" +
            "{\n" +
            "    public sealed class Item\n" +
            "    {\n" +
            "        public Item(int id, string label) { this.id = id; this.label = label; }\n" +
            "        public int id { get; }\n" +
            "        public string label { get; }\n" +
            "    }\n" +
            "    partial class Counter : IRenderHarness\n" +
            "    {\n" +
            "        private readonly Reference<bool> _visible = Reactive.Reference(true);\n" +
            "        private readonly Reference<string> _message = Reactive.Reference(\"hi\");\n" +
            "        private readonly Reference<IReadOnlyList<Item>> _items =\n" +
            "            Reactive.Reference<IReadOnlyList<Item>>(new List<Item> { new Item(1, \"a\"), new Item(2, \"b\") });\n" +
            "        private int _frozenReads;\n" +
            "        public bool visible => _visible.Value;\n" +
            "        public string message => _message.Value;\n" +
            "        public IReadOnlyList<Item> items => _items.Value;\n" +
            "        public string frozen { get { _frozenReads++; return \"frozen\"; } }\n" +
            "        int IRenderHarness.CacheSize => RenderCacheSize;\n" +
            "        object? IRenderHarness.Invoke(object?[] cache) => Render(this, cache);\n" +
            "        bool IRenderHarness.Visible { set => _visible.Value = value; }\n" +
            "        string IRenderHarness.Message { set => _message.Value = value; }\n" +
            "        int IRenderHarness.FrozenReadCount => _frozenReads;\n" +
            "        void IRenderHarness.SetItems(params (int Id, string Label)[] entries)\n" +
            "        {\n" +
            "            var list = new List<Item>(entries.Length);\n" +
            "            foreach (var entry in entries) { list.Add(new Item(entry.Id, entry.Label)); }\n" +
            "            _items.Value = list;\n" +
            "        }\n" +
            "    }\n" +
            "}\n";

        private readonly Type _counterType;

        public CompiledCounter()
        {
            var generated = CompiledRenderSupport.Generate("Counter", Template);
            // The generated render must bind by name to the runtime helper surface under test.
            generated.ShouldContain("using static global::Assimalign.Vue.RuntimeCore.RenderHelpers;");
            generated.ShouldContain("_createElementBlock(_openBlock()");
            var assembly = Assembly.Load(CompiledRenderSupport.CompileToAssembly(generated, HandWrittenHalf));
            _counterType = assembly.GetType("Demo.Counter")
                ?? throw new InvalidOperationException("The compiled assembly did not contain Demo.Counter.");
        }

        /// <summary>Creates a fresh compiled component instance behind the harness interface.</summary>
        public IRenderHarness CreateInstance()
            => (IRenderHarness)Activator.CreateInstance(_counterType, nonPublic: true)!;
    }
}

/// <summary>
/// Shared codegen-plus-Roslyn harness: runs the real source generator over a <c>.viu</c> template and
/// compiles the generated render body against the runtime helper surface. Reused by the execution fixture
/// and the compile-binding tests. The compiler/parser assemblies are deliberately kept out of the
/// generated code's reference set — the generated render is pure runtime C#.
/// </summary>
internal static class CompiledRenderSupport
{
    private const string RootNamespace = "Demo";
    private const string ProjectDirectory = "C:/proj";

    /// <summary>Runs the generator over one <c>@template</c> and returns the generated partial-class source.</summary>
    /// <param name="componentName">The <c>.viu</c> base name (also the generated class name).</param>
    /// <param name="template">The <c>.viu</c> source.</param>
    internal static string Generate(string componentName, string template)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var file = new InMemoryAdditionalText($"{ProjectDirectory}/{componentName}.viu", template);
        var globalOptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.RootNamespace"] = RootNamespace,
            ["build_property.ProjectDir"] = ProjectDirectory,
        };
        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { new SingleFileComponentGenerator().AsSourceGenerator() },
            additionalTexts: ImmutableArray.Create<AdditionalText>(file),
            parseOptions: parseOptions,
            optionsProvider: new InMemoryOptionsProvider(globalOptions));

        var compilation = CSharpCompilation.Create(
            "GeneratorInput",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();
        return result.GeneratedSources
            .Single(source => source.HintName.EndsWith($"{componentName}.SingleFileComponent.g.cs", StringComparison.Ordinal))
            .SourceText.ToString();
    }

    /// <summary>
    /// Semantically compiles the generated render body together with a hand-written partial-class half
    /// against the runtime helper surface, returning the emitted assembly bytes. Throws with the full
    /// diagnostics when binding fails — this is the by-name helper contract's compile-time proof.
    /// </summary>
    /// <param name="generatedSource">The generator output.</param>
    /// <param name="handWrittenHalf">The other half of the partial class (state/members the render binds to).</param>
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
                "The generated render body failed to compile against the runtime helper surface:" +
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
            if (!string.IsNullOrEmpty(location) && seen.Add(location))
            {
                references.Add(MetadataReference.CreateFromFile(location));
            }
        }

        // The framework's trusted platform assemblies plus exactly the Vuecs assemblies the generated
        // render and its hand-written half bind against — the runtime helper surface (RuntimeCore),
        // Reactivity, Shared (looked up by name so no linked shared type is referenced here), and this
        // test assembly for the harness interface. The compiler/parser assemblies are deliberately NOT
        // referenced: the generated render is pure runtime C#.
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
        // The generator emits `using static ...RuntimeDom.DomRenderHelpers;` in every render-bearing .viu
        // ([V01.01.04.09]), so the DOM helper surface must be referenced for the generated body to bind —
        // even though these runtime-core templates use no DOM directive.
        Add(typeof(Assimalign.Vue.RuntimeDom.DomRenderHelpers).Assembly.Location);
        Add(typeof(Reactive).Assembly.Location);
        Add(typeof(IRenderHarness).Assembly.Location);
        var shared = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Assimalign.Vue.Shared", StringComparison.Ordinal));
        Add(shared?.Location);
        return references;
    }
}

/// <summary>
/// The control surface the hand-written half of the compiled <c>Counter</c> implements, so the test drives
/// the reflectively-loaded component without per-member reflection. Defined in this test assembly and
/// referenced by the Roslyn compilation, so the compiled type's interface identity matches this one.
/// </summary>
public interface IRenderHarness
{
    /// <summary>The component's <c>RenderCacheSize</c> — the per-instance <c>_cache</c> length.</summary>
    int CacheSize { get; }

    /// <summary>Runs the generated static <c>Render(this, cache)</c> and returns its <c>object?</c> result.</summary>
    /// <param name="cache">The stable per-instance render cache.</param>
    object? Invoke(object?[] cache);

    /// <summary>Sets the <c>v-if</c> condition (a reactive write that drives a re-render).</summary>
    bool Visible { set; }

    /// <summary>Sets the interpolated message (a reactive write that drives a re-render).</summary>
    string Message { set; }

    /// <summary>Replaces the keyed <c>v-for</c> items (a reactive write that drives a re-render).</summary>
    /// <param name="entries">The new items as (id, label) pairs.</param>
    void SetItems(params (int Id, string Label)[] entries);

    /// <summary>The number of times the <c>v-once</c> source getter has been read.</summary>
    int FrozenReadCount { get; }
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
