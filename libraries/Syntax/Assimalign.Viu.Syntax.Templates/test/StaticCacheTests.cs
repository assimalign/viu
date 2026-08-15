using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.CSharp;

using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Tests for the static-caching walk (<see cref="StaticCache"/>) and the DOM stringification pass
/// (<see cref="StaticStringifier"/>) — [V01.01.05.07], specified by <c>[SFC-OPT-1]</c>–<c>[SFC-OPT-4]</c>.
/// The corpus pins eligibility (what caches vs what does not: dynamic keys, refs, runtime
/// directives, component boundaries), the numeric stringification thresholds (20 nodes / 5 elements with
/// bindings), WHATWG fragment-escaping of the serialized HTML, void-tag serialization, the opt-out flag,
/// and the value-equatable/deterministic output the incremental generator relies on.
/// </summary>
public class StaticCacheTests
{
    // ---- eligibility: what caches ----

    [Fact]
    public void StaticSubtree_IsCachedAndMarkedCached()
    {
        // A fully static nested element is cached (created once, reused) and marked PatchFlags.Cached so the
        // runtime diff skips it. The root element itself is never cached (parent fallthrough attributes).
        var result = Transform("<div><span>static</span></div>");

        var cachedSpan = ChildCodegen(result, 0).ShouldBeOfType<CacheExpression>();
        var vnode = cachedSpan.Value.ShouldBeOfType<VirtualNodeCall>();
        vnode.PatchFlag.ShouldBe(PatchFlags.Cached);
        result.Cached.Count.ShouldBe(1);
    }

    [Fact]
    public void RootElement_ItselfIsNeverCached()
    {
        // isSingleElementRoot: the single root element may receive fallthrough attributes, so it stays a
        // live block even when fully static.
        var result = Transform("<div><span>static</span></div>");

        result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().IsBlock.ShouldBeTrue();
    }

    [Fact]
    public void StaticProperties_OnDynamicChildElement_AreCached()
    {
        // The element has a dynamic child (TEXT flag) so its subtree is not cached, but its static props
        // object is: both cached subtrees and cached props objects route through the same per-instance
        // slot (see docs/DESIGN.md). The root element's own props
        // are eligible even though the root vnode is not.
        var result = Transform("<div class=\"card\">{{ msg }}</div>", prefixIdentifiers: true);

        var vnode = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>();
        vnode.PatchFlag.ShouldBe(PatchFlags.Text);
        vnode.Properties.ShouldBeOfType<CacheExpression>();
        result.Cached.Count.ShouldBe(1);
    }

    // ---- eligibility: what does NOT cache ----

    [Fact]
    public void DynamicKey_IsNeverTreatedAsStatic()
    {
        // A :key makes the element a block; a block (that is not svg/foreignObject/math) is never constant.
        var result = Transform("<div><span :key=\"k\">x</span></div>", prefixIdentifiers: true);

        ChildCodegen(result, 0).ShouldBeOfType<VirtualNodeCall>().IsBlock.ShouldBeTrue();
        result.Cached.Count.ShouldBe(0);
    }

    [Fact]
    public void TemplateReference_IsNeverTreatedAsStaticSubtree()
    {
        // A ref emits NEED_PATCH, so the subtree is not cached (the ref must run every mount).
        var result = Transform("<div><span ref=\"r\">x</span></div>");

        var codegen = ChildCodegen(result, 0);
        codegen.ShouldBeOfType<VirtualNodeCall>().PatchFlag.ShouldBe(PatchFlags.NeedPatch);
    }

    [Fact]
    public void RuntimeDirective_IsNeverTreatedAsStaticSubtree()
    {
        // A custom directive forces runtime work (NEED_PATCH + withDirectives), so the subtree is not cached.
        var result = Transform("<div><span v-custom=\"x\">y</span></div>", prefixIdentifiers: true);

        ChildCodegen(result, 0).ShouldNotBeOfType<CacheExpression>();
    }

    [Fact]
    public void ComponentBoundary_IsNotCachedAsElement()
    {
        // A component is not a plain element, so it is never cached as a static subtree.
        var result = Transform("<div><Comp></Comp></div>");

        ChildCodegen(result, 0).ShouldBeOfType<VirtualNodeCall>().IsComponent.ShouldBeTrue();
    }

    [Fact]
    public void DynamicChild_PreventsSubtreeCaching()
    {
        // An interpolation child poisons the parent's constant type, so the parent is not cached.
        var result = Transform("<div><span>{{ dynamic }}</span></div>", prefixIdentifiers: true);

        ChildCodegen(result, 0).ShouldBeOfType<VirtualNodeCall>().PatchFlag.ShouldBe(PatchFlags.Text);
        result.Cached.Count.ShouldBe(0);
    }

    [Fact]
    public void SingleChildVIf_StaticProperties_DoNotReserveOrphanedCacheSlot()
    {
        // [V01.01.06.14]/[SFC-OPT-1] The keyed v-if branch owns a snapshotted vnode call. A
        // side-table-only property cache would be unreachable from emitted code and would shift every
        // later slot, so the compiler must not count it.
        RenderFunctionEmitterResult emitted = Emit(
            "<main><div class=\"conditional\" v-if=\"show\">visible</div>" +
            "<p>cached sibling</p></main>",
            prefixIdentifiers: true);

        emitted.CacheSlotCount.ShouldBe(1);
        emitted.Code.ShouldContain(
            "frame.GetOrAddCache<global::Assimalign.Viu.Components.VirtualNode?>(0");
        emitted.Code.ShouldNotContain(
            "frame.GetOrAddCache<global::Assimalign.Viu.Components.VirtualNode?>(1");
    }

    // ---- the opt-out flag ----

    [Fact]
    public void HoistStaticOff_CachesNothing()
    {
        // The debugging opt-out: with HoistStatic off, the identical template produces no caches, and the
        // vnode structure is otherwise identical (same span vnode, just uncached) — so optimized and
        // unoptimized render the same tree.
        var optimized = Emit("<div><span>static</span></div>", hoistStatic: true);
        var unoptimized = Emit("<div><span>static</span></div>", hoistStatic: false);

        optimized.Code.ShouldContain("frame.GetOrAddCache<");
        optimized.Code.ShouldContain("(-1) /* CACHED */");
        unoptimized.Code.ShouldNotContain("frame.GetOrAddCache<");
        unoptimized.Code.ShouldNotContain("CACHED");
        unoptimized.CacheSlotCount.ShouldBe(0);
    }

    // ---- stringification thresholds (20 nodes / 5 elements-with-bindings, [SFC-OPT-2]) ----

    [Fact]
    public void ContiguousStaticRun_AtNodeThreshold_CollapsesToOneStaticVNode()
    {
        // 20 contiguous static nodes reach StringifyThresholds.NODE_COUNT and collapse into a single
        // StaticNode carrying the serialized HTML, created once (per the run-count pin below).
        var code = Emit(Wrapped("<div></div>", 20)).Code;

        StaticVNodeCount(code).ShouldBe(1);
        code.ShouldContain("new global::Assimalign.Viu.Components.StaticNode(");
        code.ShouldContain("MarkupFormat.Html");
    }

    [Fact]
    public void JustBelowNodeThreshold_DoesNotStringify()
    {
        // 19 nodes stay below NODE_COUNT (and below ELEMENT_WITH_BINDING_COUNT), so no stringification —
        // each element is still individually cached instead.
        var result = TransformSource(Wrapped("<div></div>", 19));
        var code = RenderFunctionEmitter.Emit(result).Code;

        code.ShouldNotContain(".StaticNode(");
        result.Cached.Count.ShouldBe(19);
    }

    [Fact]
    public void FiveElementsWithBindings_ReachBindingThreshold_AndStringify()
    {
        // 5 elements each carrying an attribute binding reach StringifyThresholds.ELEMENT_WITH_BINDING_COUNT
        // even though the node count (5) is well under NODE_COUNT.
        var code = Emit(Wrapped("<div id=\"x\"></div>", 5)).Code;

        StaticVNodeCount(code).ShouldBe(1);
        code.ShouldContain("MarkupFormat.Html");
    }

    [Fact]
    public void FourElementsWithBindings_StayBelowBindingThreshold()
    {
        // 4 elements with bindings is under the threshold; nothing stringifies.
        Emit(Wrapped("<div id=\"x\"></div>", 4)).Code.ShouldNotContain(".StaticNode(");
    }

    [Fact]
    public void StringifiedRun_SerializesExactlyOnce()
    {
        // The run-count pin: a collapsed run produces exactly one static vnode, cached in one slot — the
        // whole run marshals across the interop boundary once, not per node.
        var result = TransformSource(Wrapped("<p>x</p>", 20));
        var code = RenderFunctionEmitter.Emit(result).Code;

        StaticVNodeCount(code).ShouldBe(1);
    }

    [Fact]
    public void MergedCacheSlots_StayReserved_NotCompacted()
    {
        // Documented divergence: after merging a run, the merged nodes' cache slots stay reserved rather
        // than being compacted and re-indexed (which would require mutating immutable CacheExpressions).
        var result = TransformSource(Wrapped("<div></div>", 20));

        result.Cached.Count.ShouldBe(20);
    }

    // ---- serialization: escaping and void tags (WHATWG fragment serialization) ----

    [Fact]
    public void SerializedHtml_EscapesMarkupSensitiveCharacters()
    {
        // Escaping ([SFC-OPT-4]): & < > " ' are escaped so the innerHTML round-trips to the same
        // DOM as node-by-node creation.
        var code = Emit(Wrapped("<i>a &amp; b &lt; c</i>", 20)).Code;

        code.ShouldContain("a &amp; b &lt; c");
    }

    [Fact]
    public void SerializedHtml_OmitsClosingTagForVoidElements()
    {
        // Void elements serialize with no end tag.
        var code = Emit(Wrapped("<br>", 20)).Code;

        code.ShouldContain("new global::Assimalign.Viu.Components.StaticNode(");
        code.ShouldNotContain("</br>");
    }

    [Fact]
    public void SerializedHtml_KeepsStaticAttributes()
    {
        var code = Emit(Wrapped("<div id=\"x\"></div>", 20)).Code;

        code.ShouldContain("<div id=\\\"x\\\"></div>");
    }

    [Fact]
    public void MathMlKnownStaticAttributes_StringifyAsExtensibleMarkupLanguage()
    {
        var code = Emit(
            "<math><mi mathcolor=\"red\">a</mi><mi mathcolor=\"red\">b</mi>" +
            "<mi mathcolor=\"red\">c</mi><mi mathcolor=\"red\">d</mi>" +
            "<mi mathcolor=\"red\">e</mi></math>").Code;

        StaticVNodeCount(code).ShouldBe(1);
        code.ShouldContain("MarkupFormat.ExtensibleMarkupLanguage");
        code.ShouldContain("<mi mathcolor=\\\"red\\\">a</mi>");
    }

    [Fact]
    public void MathMlUnknownStaticAttribute_BailsOutOfStringification()
    {
        var code = Emit(
            "<math><mi future-attribute=\"x\">a</mi><mi future-attribute=\"x\">b</mi>" +
            "<mi future-attribute=\"x\">c</mi><mi future-attribute=\"x\">d</mi>" +
            "<mi future-attribute=\"x\">e</mi></math>").Code;

        code.ShouldNotContain(".StaticNode(");
    }

    [Fact]
    public void MathMlDynamicKnownAttribute_BailsOutOfStringification()
    {
        var code = Emit(
            "<math><mi :mathcolor=\"color\">a</mi><mi :mathcolor=\"color\">b</mi>" +
            "<mi :mathcolor=\"color\">c</mi><mi :mathcolor=\"color\">d</mi>" +
            "<mi :mathcolor=\"color\">e</mi></math>",
            prefixIdentifiers: true).Code;

        code.ShouldNotContain(".StaticNode(");
    }

    [Theory]
    [InlineData("svg", "foreignObject", "")]
    [InlineData("svg", "desc", "")]
    [InlineData("svg", "title", "")]
    [InlineData("math", "mtext", "")]
    [InlineData("math", "annotation-xml", " encoding=\"text/html\"")]
    public void ForeignContentHtmlIntegrationPoint_StaticChildrenUseHtmlFragmentFormat(
        string rootTag,
        string integrationTag,
        string integrationAttributes)
    {
        string children = string.Concat(Enumerable.Repeat("<span id=\"x\"></span>", 5));
        string source = string.Concat(
            "<",
            rootTag,
            "><",
            integrationTag,
            integrationAttributes,
            ">",
            children,
            "{{ tail }}</",
            integrationTag,
            "></",
            rootTag,
            ">");

        string code = Emit(source, prefixIdentifiers: true).Code;

        StaticVNodeCount(code).ShouldBe(1);
        OccurrenceCount(code, "MarkupFormat.Html").ShouldBe(1);
        OccurrenceCount(code, "MarkupFormat.ExtensibleMarkupLanguage").ShouldBe(0);
    }

    [Fact]
    public void AdjacentHtmlAndSvgStaticRuns_AreSplitByFragmentFormat()
    {
        string htmlChildren = string.Concat(
            Enumerable.Repeat("<div id=\"html\"></div>", 5));
        string svgChildren = string.Concat(
            Enumerable.Repeat("<svg viewBox=\"0 0 1 1\"></svg>", 5));
        string code = Emit(
            string.Concat("<section>", htmlChildren, svgChildren, "{{ tail }}</section>"),
            prefixIdentifiers: true).Code;

        StaticVNodeCount(code).ShouldBe(2);
        OccurrenceCount(code, "MarkupFormat.Html").ShouldBe(1);
        OccurrenceCount(code, "MarkupFormat.ExtensibleMarkupLanguage").ShouldBe(1);
    }

    // ---- slot content bails stringification ----

    [Fact]
    public void SlotContent_IsNotStringified()
    {
        // Stringification bails inside a slot scope: a component's slot content is not folded
        // into an innerHTML string.
        Emit(WrappedComponent("<div></div>", 20)).Code.ShouldNotContain(".StaticNode(");
    }

    // ---- emitter snapshots ----

    [Fact]
    public void CachedSubtree_EmitsRenderCacheAssignmentWithCachedFlag()
    {
        string code = Emit("<div><span>static</span></div>").Code;

        code.ShouldContain("frame.GetOrAddCache<global::Assimalign.Viu.Components.VirtualNode?>");
        code.ShouldContain("(-1) /* CACHED */");
        code.ShouldContain("new global::Assimalign.Viu.Components.ElementNode(");
    }

    [Fact]
    public void TwoStaticSiblings_BelowThreshold_EmitTwoCacheSlots()
    {
        RenderFunctionEmitterResult emitted = Emit("<section><p>a</p><p>b</p></section>");

        emitted.Code.ShouldContain("frame.GetOrAddCache<global::Assimalign.Viu.Components.VirtualNode?>(0");
        emitted.Code.ShouldContain("frame.GetOrAddCache<global::Assimalign.Viu.Components.VirtualNode?>(1");
        emitted.CacheSlotCount.ShouldBe(2);
    }

    [Fact]
    public void CachedStaticProperties_EmitRenderCacheAssignment()
    {
        string code = Emit("<div class=\"card\">{{ msg }}</div>", prefixIdentifiers: true).Code;

        code.ShouldContain("frame.GetOrAddCache<global::System.Collections.Generic.IReadOnlyDictionary<string, object?>>(0");
        code.ShouldContain("[\"class\"] = \"card\"");
        code.ShouldContain("DisplayStringFormatter.ToDisplayString(component.msg)");
    }

    // ---- the optimized output is valid C# ----

    [Theory]
    [InlineData("<div><span>static</span></div>")]                         // cached subtree
    [InlineData("<section><p>a</p><p>b</p></section>")]                    // two cached siblings
    [InlineData("<div class=\"card\">{{ msg }}</div>")]                    // cached static props
    [InlineData("<section><div id=\"x\"></div><div id=\"y\"></div><div id=\"z\"></div><div></div><div></div></section>")] // stringified run (binding threshold)
    [InlineData("<Comp><span>a</span><span>b</span></Comp>")]             // cached content inside a component slot
    public void OptimizedOutput_ParsesAsValidCSharp(string source)
    {
        // Every cached/stringified render body must be syntactically valid C# in the render-method shape the
        // generator emits (the serialized HTML rides through SymbolDisplay.FormatLiteral, so any content is a
        // well-formed literal).
        var code = Emit(source, prefixIdentifiers: true).Code;
        var unit =
            "internal static class RenderProbe { internal static object? Render(object component, object frame) {\n" +
            code +
            "} }";

        var tree = CSharpSyntaxTree.ParseText(unit, new CSharpParseOptions(LanguageVersion.Preview));
        tree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == RoslynDiagnosticSeverity.Error)
            .ShouldBeEmpty(customMessage: code);
    }

    [Fact]
    public void StringifiedRun_ForcedByBindingThreshold_ParsesWithEscapedHtmlLiteral()
    {
        // A run whose serialized HTML carries quotes and ampersands still produces a valid C# string literal.
        var code = Emit(Wrapped("<p class=\"lead\">Hi &amp; bye</p>", 20)).Code;
        var unit =
            "internal static class RenderProbe { internal static object? Render(object component, object frame) {\n" +
            code +
            "} }";

        code.ShouldContain("<p class=\\\"lead\\\">Hi &amp; bye</p>");
        CSharpSyntaxTree.ParseText(unit, new CSharpParseOptions(LanguageVersion.Preview))
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == RoslynDiagnosticSeverity.Error)
            .ShouldBeEmpty(customMessage: code);
    }

    // ---- determinism / value equality (incremental generator caching contract) ----

    [Fact]
    public void Caching_IsDeterministicAndValueEquatable()
    {
        const string source = "<section><span>a</span><span>b</span></section>";
        var first = TransformSource(source);
        var second = TransformSource(source);

        first.CodegenNode.ShouldBe(second.CodegenNode);
        RenderFunctionEmitter.Emit(first).Code.ShouldBe(RenderFunctionEmitter.Emit(second).Code);
    }

    // ---- harness ----

    private static TransformResult TransformSource(string source, bool prefixIdentifiers = false)
    {
        var root = TemplateParser.Parse(source, ParserOptions.CreateHtml());
        var options = TransformOptions.CreateDom();
        options.HoistStatic = true;
        if (prefixIdentifiers)
        {
            options.PrefixIdentifiers = true;
            options.BindingMetadata = BindingMetadata.Empty;
        }

        return Transformer.Transform(root, options);
    }

    private static TransformResult Transform(string source, bool prefixIdentifiers = false)
        => TransformSource(source, prefixIdentifiers);

    private static RenderFunctionEmitterResult Emit(string source, bool hoistStatic = true, bool prefixIdentifiers = false)
    {
        var root = TemplateParser.Parse(source, ParserOptions.CreateHtml());
        var options = TransformOptions.CreateDom();
        options.HoistStatic = hoistStatic;
        if (prefixIdentifiers)
        {
            options.PrefixIdentifiers = true;
            options.BindingMetadata = BindingMetadata.Empty;
        }

        return RenderFunctionEmitter.Emit(Transformer.Transform(root, options));
    }

    // The codegen node of the index-th child of the single root element.
    private static TemplateSyntaxNode? ChildCodegen(TransformResult result, int index)
    {
        var root = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>();
        var children = (SyntaxList<TemplateChildNode>)root.Children!;
        return result.GetCodegenNode(children[index]);
    }

    private static string Wrapped(string unit, int count)
        => "<section>" + string.Concat(Enumerable.Repeat(unit, count)) + "</section>";

    private static string WrappedComponent(string unit, int count)
        => "<Comp>" + string.Concat(Enumerable.Repeat(unit, count)) + "</Comp>";

    private static int StaticVNodeCount(string code) => OccurrenceCount(
        code,
        "new global::Assimalign.Viu.Components.StaticNode(");

    private static int OccurrenceCount(string value, string marker)
    {
        var occurrences = 0;
        var index = 0;
        while ((index = value.IndexOf(marker, index, System.StringComparison.Ordinal)) >= 0)
        {
            occurrences++;
            index += marker.Length;
        }

        return occurrences;
    }
}
