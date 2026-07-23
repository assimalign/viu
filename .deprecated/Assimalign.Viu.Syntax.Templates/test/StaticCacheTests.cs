using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.CSharp;

using Assimalign.Viu.Shared;

using Shouldly;

using Xunit;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Tests for the static-caching walk (<see cref="StaticCache"/>) and the DOM stringification pass
/// (<see cref="StaticStringifier"/>) — [V01.01.05.07], the C# port of Vue 3.5's
/// <c>@vue/compiler-core</c> <c>cacheStatic.ts</c> and <c>@vue/compiler-dom</c> <c>stringifyStatic.ts</c>.
/// The corpus pins upstream eligibility (what caches vs what does not: dynamic keys, refs, runtime
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
        var vnode = cachedSpan.Value.ShouldBeOfType<VNodeCall>();
        vnode.PatchFlag.ShouldBe(PatchFlags.Cached);
        result.Cached.Count.ShouldBe(1);
    }

    [Fact]
    public void RootElement_ItselfIsNeverCached()
    {
        // isSingleElementRoot: the single root element may receive fallthrough attributes, so it stays a
        // live block even when fully static.
        var result = Transform("<div><span>static</span></div>");

        result.CodegenNode.ShouldBeOfType<VNodeCall>().IsBlock.ShouldBeTrue();
    }

    [Fact]
    public void StaticProps_OnDynamicChildElement_AreCached()
    {
        // The element has a dynamic child (TEXT flag) so its subtree is not cached, but its static props
        // object is (upstream hoists it; Viu caches it — see docs/DESIGN.md). The root element's own props
        // are eligible even though the root vnode is not.
        var result = Transform("<div class=\"card\">{{ msg }}</div>", prefixIdentifiers: true);

        var vnode = result.CodegenNode.ShouldBeOfType<VNodeCall>();
        vnode.PatchFlag.ShouldBe(PatchFlags.Text);
        vnode.Props.ShouldBeOfType<CacheExpression>();
        result.Cached.Count.ShouldBe(1);
    }

    // ---- eligibility: what does NOT cache ----

    [Fact]
    public void DynamicKey_IsNeverTreatedAsStatic()
    {
        // A :key makes the element a block; a block (that is not svg/foreignObject/math) is never constant.
        var result = Transform("<div><span :key=\"k\">x</span></div>", prefixIdentifiers: true);

        ChildCodegen(result, 0).ShouldBeOfType<VNodeCall>().IsBlock.ShouldBeTrue();
        result.Cached.Count.ShouldBe(0);
    }

    [Fact]
    public void TemplateReference_IsNeverTreatedAsStaticSubtree()
    {
        // A ref emits NEED_PATCH, so the subtree is not cached (the ref must run every mount).
        var result = Transform("<div><span ref=\"r\">x</span></div>");

        var codegen = ChildCodegen(result, 0);
        codegen.ShouldBeOfType<VNodeCall>().PatchFlag.ShouldBe(PatchFlags.NeedPatch);
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

        ChildCodegen(result, 0).ShouldBeOfType<VNodeCall>().IsComponent.ShouldBeTrue();
    }

    [Fact]
    public void DynamicChild_PreventsSubtreeCaching()
    {
        // An interpolation child poisons the parent's constant type, so the parent is not cached.
        var result = Transform("<div><span>{{ dynamic }}</span></div>", prefixIdentifiers: true);

        ChildCodegen(result, 0).ShouldBeOfType<VNodeCall>().PatchFlag.ShouldBe(PatchFlags.Text);
        result.Cached.Count.ShouldBe(0);
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

        optimized.Code.ShouldContain("_cache[0] ??=");
        optimized.Code.ShouldContain("-1 /* CACHED */");
        unoptimized.Code.ShouldNotContain("_cache");
        unoptimized.Code.ShouldNotContain("CACHED");
        unoptimized.CacheSlotCount.ShouldBe(0);
    }

    // ---- stringification thresholds (upstream 20 nodes / 5 elements-with-bindings) ----

    [Fact]
    public void ContiguousStaticRun_AtNodeThreshold_CollapsesToOneStaticVNode()
    {
        // 20 contiguous static nodes reach StringifyThresholds.NODE_COUNT and collapse into a single
        // createStaticVNode carrying the serialized HTML, created once (per the run-count pin below).
        var code = Emit(Wrapped("<div></div>", 20)).Code;

        StaticVNodeCount(code).ShouldBe(1);
        code.ShouldContain("_createStaticVNode(");
        code.ShouldContain(", 20)");
    }

    [Fact]
    public void JustBelowNodeThreshold_DoesNotStringify()
    {
        // 19 nodes stay below NODE_COUNT (and below ELEMENT_WITH_BINDING_COUNT), so no stringification —
        // each element is still individually cached instead.
        var result = TransformSource(Wrapped("<div></div>", 19));
        var code = RenderFunctionEmitter.Emit(result).Code;

        code.ShouldNotContain("_createStaticVNode");
        result.Cached.Count.ShouldBe(19);
    }

    [Fact]
    public void FiveElementsWithBindings_ReachBindingThreshold_AndStringify()
    {
        // 5 elements each carrying an attribute binding reach StringifyThresholds.ELEMENT_WITH_BINDING_COUNT
        // even though the node count (5) is well under NODE_COUNT.
        var code = Emit(Wrapped("<div id=\"x\"></div>", 5)).Code;

        StaticVNodeCount(code).ShouldBe(1);
        code.ShouldContain(", 5)");
    }

    [Fact]
    public void FourElementsWithBindings_StayBelowBindingThreshold()
    {
        // 4 elements with bindings is under the threshold; nothing stringifies.
        Emit(Wrapped("<div id=\"x\"></div>", 4)).Code.ShouldNotContain("_createStaticVNode");
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
        // escapeHtml parity (@vue/shared): & < > " ' are escaped so the innerHTML round-trips to the same
        // DOM as node-by-node creation.
        var code = Emit(Wrapped("<i>a &amp; b &lt; c</i>", 20)).Code;

        code.ShouldContain("a &amp; b &lt; c");
    }

    [Fact]
    public void SerializedHtml_OmitsClosingTagForVoidElements()
    {
        // Void elements serialize with no end tag (upstream isVoidTag).
        var code = Emit(Wrapped("<br>", 20)).Code;

        code.ShouldContain("_createStaticVNode(");
        code.ShouldNotContain("</br>");
    }

    [Fact]
    public void SerializedHtml_KeepsStaticAttributes()
    {
        var code = Emit(Wrapped("<div id=\"x\"></div>", 20)).Code;

        code.ShouldContain("<div id=\\\"x\\\"></div>");
    }

    // ---- slot content bails stringification ----

    [Fact]
    public void SlotContent_IsNotStringified()
    {
        // Upstream bails stringification when scopes.vSlot > 0: a component's slot content is not folded
        // into an innerHTML string.
        Emit(WrappedComponent("<div></div>", 20)).Code.ShouldNotContain("_createStaticVNode");
    }

    // ---- emitter snapshots ----

    [Fact]
    public void CachedSubtree_EmitsRenderCacheAssignmentWithCachedFlag()
    {
        Emit("<div><span>static</span></div>").Code.ShouldBeCode(
"""
return _createElementBlock(_openBlock(), "div", null, new object?[] { (_cache[0] ??= _createElementVNode("span", null, "static", -1 /* CACHED */)) });

""");
    }

    [Fact]
    public void TwoStaticSiblings_BelowThreshold_EmitTwoCacheSlots()
    {
        Emit("<section><p>a</p><p>b</p></section>").Code.ShouldBeCode(
"""
return _createElementBlock(_openBlock(), "section", null, new object?[] { (_cache[0] ??= _createElementVNode("p", null, "a", -1 /* CACHED */)), (_cache[1] ??= _createElementVNode("p", null, "b", -1 /* CACHED */)) });

""");
    }

    [Fact]
    public void CachedStaticProps_EmitRenderCacheAssignment()
    {
        Emit("<div class=\"card\">{{ msg }}</div>", prefixIdentifiers: true).Code.ShouldBeCode(
"""
return _createElementBlock(_openBlock(), "div", (_cache[0] ??= _createProps(("class", "card"))), _toDisplayString(_ctx.msg), 1 /* TEXT */);

""");
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
            "internal static class RenderProbe { internal static object? Render(object _ctx, object?[] _cache) {\n" +
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
            "internal static class RenderProbe { internal static object? Render(object _ctx, object?[] _cache) {\n" +
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
        var root = result.CodegenNode.ShouldBeOfType<VNodeCall>();
        var children = (SyntaxList<TemplateChildNode>)root.Children!;
        return result.GetCodegenNode(children[index]);
    }

    private static string Wrapped(string unit, int count)
        => "<section>" + string.Concat(Enumerable.Repeat(unit, count)) + "</section>";

    private static string WrappedComponent(string unit, int count)
        => "<Comp>" + string.Concat(Enumerable.Repeat(unit, count)) + "</Comp>";

    private static int StaticVNodeCount(string code)
    {
        var occurrences = 0;
        var index = 0;
        while ((index = code.IndexOf("_createStaticVNode(", index, System.StringComparison.Ordinal)) >= 0)
        {
            occurrences++;
            index += "_createStaticVNode(".Length;
        }

        return occurrences;
    }
}
