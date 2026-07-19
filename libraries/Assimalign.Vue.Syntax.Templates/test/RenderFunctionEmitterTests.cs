using System.Linq;

using Microsoft.CodeAnalysis.CSharp;

using Shouldly;

using Xunit;

// The test namespace sits under Assimalign.Vue.Syntax, whose DiagnosticSeverity shadows Roslyn's;
// alias the Roslyn enum used by the parse-validity checks.
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// Snapshot, parse-validity, and determinism tests for <see cref="RenderFunctionEmitter"/> — the C# port
/// of Vue 3.5's <c>generate()</c> (<c>@vue/compiler-core</c> <c>codegen.ts</c>; upstream expectations in
/// <c>packages/compiler-core/__tests__/codegen.spec.ts</c> and the <c>__snapshots__</c> of
/// <c>compile.spec.ts</c>). The snapshots pin BOTH upstream output parity (helper spelling and argument
/// order per <c>helperNameMap</c>) and the Vuecs C# divergences documented in <c>docs/DESIGN.md</c>:
/// block sequences ride on argument evaluation order (<c>_createElementBlock(_openBlock(), ...)</c>
/// instead of the comma operator), object literals emit through <c>_createProps</c>, child arrays emit as
/// <c>new object?[] { ... }</c>, handler values wrap in <c>_withHandler</c>, and cache slots use
/// <c>??=</c>. These emitted names ARE the by-name runtime-helper contract the issue (#52) requires this
/// library to pin — the runtime never gets referenced from here.
/// </summary>
public class RenderFunctionEmitterTests
{
    // ---- element / props / patch flags (upstream compile.spec.ts element snapshots) ----

    [Fact]
    public void Element_WithMixedProps_EmitsPropsObjectPatchFlagAndDynamicProps()
    {
        // Patch-flag numeric parity: TEXT|PROPS = 9 with the upstream PatchFlagNames comment, and the
        // dynamicProps array emits verbatim as a C# collection expression targeting string[].
        var emitted = EmitPrefixed("<div :id=\"dynamicId\" class=\"static\">{{ message }}</div>");

        emitted.Code.ShouldBeCode(
"""
return _createElementBlock(_openBlock(), "div", _createProps(
    ("id", _ctx.dynamicId),
    ("class", "static")
), _toDisplayString(_ctx.message), 9 /* TEXT, PROPS */, ["id"]);

""");
        emitted.CacheSlotCount.ShouldBe(0);
    }

    [Fact]
    public void StaticElement_EmitsBlockWithoutPropsOrFlags()
    {
        // genNullableArgs parity: trailing null arguments are trimmed, so a fully static element is
        // just tag + children.
        EmitPrefixed("<div>x</div>").Code.ShouldBeCode(
"""
return _createElementBlock(_openBlock(), "div", null, "x");

""");
    }

    [Fact]
    public void ManyChildren_SplitAcrossLines()
    {
        // genNodeListAsArray parity: more than three children switch to the multiline array form.
        EmitPrefixed("<ul><li>1</li><li>2</li><li>3</li><li>{{ four }}</li></ul>").Code.ShouldBeCode(
"""
return _createElementBlock(_openBlock(), "ul", null, new object?[] {
    _createElementVNode("li", null, "1"),
    _createElementVNode("li", null, "2"),
    _createElementVNode("li", null, "3"),
    _createElementVNode("li", null, _toDisplayString(_ctx.four), 1 /* TEXT */)
});

""");
    }

    // ---- v-if (upstream vIf.spec.ts codegen expectations) ----

    [Fact]
    public void VIfChain_EmitsNestedConditionalWithBranchBlocksAndKeys()
    {
        // Each branch opens its own block with the synthetic key (0, 1, 2); the alternate of each
        // conditional is the next branch, stair-stepped exactly like upstream's needNewline layout.
        EmitPrefixed("<div v-if=\"visible\">A</div><span v-else-if=\"other\">B</span><p v-else>C</p>").Code.ShouldBeCode(
"""
return (_ctx.visible)
    ? _createElementBlock(_openBlock(), "div", _createProps(("key", 0)), "A")
    : (_ctx.other)
        ? _createElementBlock(_openBlock(), "span", _createProps(("key", 1)), "B")
        : _createElementBlock(_openBlock(), "p", _createProps(("key", 2)), "C");

""");
    }

    [Fact]
    public void LoneVIf_TerminatesChainWithCommentVNode()
    {
        // Upstream terminates a v-if without v-else with createCommentVNode("v-if", true).
        EmitPrefixed("<div v-if=\"ok\">A</div>").Code.ShouldBeCode(
"""
return (_ctx.ok)
    ? _createElementBlock(_openBlock(), "div", _createProps(("key", 0)), "A")
    : _createCommentVNode("v-if", true);

""");
    }

    // ---- v-for (upstream vFor.spec.ts codegen expectations) ----

    [Fact]
    public void KeyedVFor_EmitsDisabledTrackingFragmentWithRenderListLambda()
    {
        // The fragment opens its block with tracking disabled (_openBlock(true)); the iterator is a
        // braced lambda (upstream newline: true) whose per-item vnode is itself a keyed block.
        EmitPrefixed("<li v-for=\"item in items\" :key=\"item.id\">{{ item.label }}</li>").Code.ShouldBeCode(
"""
return _createElementBlock(_openBlock(true), _Fragment, null, _renderList(_ctx.items, (item) => {
    return _createElementBlock(_openBlock(), "li", _createProps(("key", item.id)), _toDisplayString(item.label), 1 /* TEXT */);
}), 128 /* KEYED_FRAGMENT */);

""");
    }

    [Fact]
    public void TemplateVFor_WrapsEachIterationInStableFragment()
    {
        EmitPrefixed("<template v-for=\"row in rows\"><td>{{ row }}</td><td>b</td></template>").Code.ShouldBeCode(
"""
return _createElementBlock(_openBlock(true), _Fragment, null, _renderList(_ctx.rows, (row) => {
    return _createElementBlock(_openBlock(), _Fragment, null, new object?[] { _createElementVNode("td", null, _toDisplayString(row), 1 /* TEXT */), _createElementVNode("td", null, "b") }, 64 /* STABLE_FRAGMENT */);
}), 256 /* UNKEYED_FRAGMENT */);

""");
    }

    // ---- components and slots (upstream vSlot.spec.ts / compile.spec.ts) ----

    [Fact]
    public void ComponentWithSlots_EmitsResolvePreambleWithCtxWrappersAndSlotFlag()
    {
        // The asset preamble resolves the component by name into the toValidAssetId local; slot
        // functions wrap in _withCtx; the trailing ("_", 1) entry is the STABLE SlotFlags marker.
        var emitted = EmitPrefixed(
            "<MyButton :kind=\"kind\"><template #header=\"headerProperties\"><b>{{ headerProperties }}</b></template>" +
            "<span>{{ label }}</span></MyButton>");

        emitted.Code.ShouldBeCode(
"""
var _component_MyButton = _resolveComponent("MyButton");

return _createBlock(_openBlock(), _component_MyButton, _createProps(("kind", _ctx.kind)), _createProps(
    ("header", _withCtx((headerProperties) => new object?[] { _createElementVNode("b", null, _toDisplayString(headerProperties), 1 /* TEXT */) })),
    ("default", _withCtx(() => new object?[] { _createElementVNode("span", null, _toDisplayString(_ctx.label), 1 /* TEXT */) })),
    ("_", 1)
), 8 /* PROPS */, ["kind"]);

""");
    }

    [Fact]
    public void DynamicComponent_EmitsResolveDynamicComponentBlock()
    {
        // <component :is> compiles to a resolveDynamicComponent tag inside a createBlock (issue #52
        // acceptance criterion; upstream resolveComponentType).
        EmitPrefixed("<component :is=\"viewName\"></component>").Code.ShouldBeCode(
"""
return _createBlock(_openBlock(), _resolveDynamicComponent(_ctx.viewName));

""");
    }

    [Fact]
    public void SlotOutlet_EmitsRenderSlotWithContractSlotSourceAndFallback()
    {
        // Upstream renderSlot($slots, "header", {}, fallback): `$slots` has no legal C# spelling, so
        // the contract emits _ctx.__slots (the __event precedent), and the `{}` placeholder becomes
        // the empty _createProps() (docs/DESIGN.md divergence table).
        EmitPrefixed("<slot name=\"header\"><p>fallback {{ hint }}</p></slot>").Code.ShouldBeCode(
"""
return _renderSlot(_ctx.__slots, "header", _createProps(), () => new object?[] { _createElementVNode("p", null, "fallback " + _toDisplayString(_ctx.hint), 1 /* TEXT */) });

""");
    }

    [Fact]
    public void Teleport_EmitsBuiltInHelperTagBlock()
    {
        EmitPrefixed("<Teleport to=\"body\"><div>{{ tip }}</div></Teleport>").Code.ShouldBeCode(
"""
return _createBlock(_openBlock(), _Teleport, _createProps(("to", "body")), new object?[] { _createElementVNode("div", null, _toDisplayString(_ctx.tip), 1 /* TEXT */) });

""");
    }

    // ---- fragments ----

    [Fact]
    public void MultiRoot_EmitsStableFragmentBlock()
    {
        EmitPrefixed("<div>one</div><span>{{ two }}</span>").Code.ShouldBeCode(
"""
return _createElementBlock(_openBlock(), _Fragment, null, new object?[] { _createElementVNode("div", null, "one"), _createElementVNode("span", null, _toDisplayString(_ctx.two), 1 /* TEXT */) }, 64 /* STABLE_FRAGMENT */);

""");
    }

    // ---- v-once / cache slots (upstream vOnce.spec.ts) ----

    [Fact]
    public void VOnce_EmitsCacheSlotWithPausedBlockTracking()
    {
        // Upstream: _cache[0] || (setBlockTracking(-1, true), (_cache[0] = createElementVNode(...))
        // .cacheIndex = 0, setBlockTracking(1), _cache[0]). C# has no comma operator, so the sequence
        // collapses into `_cache[0] ??= _setCache(0, _setBlockTracking(-1, true), value)` — argument
        // evaluation order pauses tracking before the value is created, and _setCache stamps the index,
        // resumes tracking, and returns the value.
        var emitted = EmitPrefixed("<div v-once><span>{{ frozen }}</span></div>");

        emitted.Code.ShouldBeCode(
"""
return (_cache[0] ??= _setCache(0, _setBlockTracking(-1, true), _createElementVNode("div", null, new object?[] { _createElementVNode("span", null, _toDisplayString(_ctx.frozen), 1 /* TEXT */) })));

""");
        emitted.CacheSlotCount.ShouldBe(1);
    }

    // ---- event handlers (the C# delegate-typing divergence) ----

    [Fact]
    public void Handlers_WrapInWithHandlerForDelegateTargetTyping()
    {
        // A C# lambda or method group has no natural type in an object-typed position, so handler
        // property values wrap in the contract helper _withHandler (docs/DESIGN.md). The inline
        // statement uses the __event parameter spelling pinned by [V01.01.05.04].
        EmitPrefixed("<button @click=\"count++\" @submit=\"save\">Go</button>").Code.ShouldBeCode(
"""
return _createElementBlock(_openBlock(), "button", _createProps(
    ("onClick", _withHandler(__event => (_ctx.count++))),
    ("onSubmit", _withHandler(_ctx.save))
), "Go", 40 /* PROPS, NEED_HYDRATION */, ["onClick", "onSubmit"]);

""");
    }

    [Fact]
    public void VoidCallHandler_EmitsStatementBlockLambda()
    {
        // A single-statement inline call handler emits as a statement-block lambda (__event => { call; })
        // rather than an expression lambda (__event => (call)): a void call has no value to parenthesize
        // and would bind no _withHandler delegate overload, so the block form — which binds
        // Action<object?> and discards any value like upstream's arrow function — is used
        // ([V01.01.05.05.01], issue #143; upstream transformOn: vuejs/core v3.5 compiler-core vOn.ts).
        EmitPrefixed("<button @click=\"save($event)\">x</button>").Code.ShouldBeCode(
"""
return _createElementBlock(_openBlock(), "button", _createProps(("onClick", _withHandler(__event => { _ctx.save(__event); }))), "x", 8 /* PROPS */, ["onClick"]);

""");
    }

    [Fact]
    public void ModifierHandler_KeepsWithModifiersUnwrapped()
    {
        // withModifiers/withKeys already give the inner lambda its delegate target type through their
        // own contract signature, so no extra _withHandler wrapper is added around them. The inline
        // handler is a call, which may be void-typed, so it emits as a statement-block lambda
        // (__event => { call; }) — the shape that binds withModifiers' Action<BrowserEvent> overload;
        // a parenthesized void call binds no overload ([V01.01.05.05.01], issue #143).
        EmitPrefixed("<button @click.stop=\"save($event)\">x</button>").Code.ShouldBeCode(
"""
return _createElementBlock(_openBlock(), "button", _createProps(("onClick", _withModifiers(__event => { _ctx.save(__event); }, ["stop"]))), "x", 8 /* PROPS */, ["onClick"]);

""");
    }

    [Fact]
    public void VModel_EmitsUpdateHandlerWithEventSpellingAndRuntimeDirective()
    {
        // The v-model assignment handler is authored by the transform with Vue's $event variable;
        // serialization maps it to the Vuecs __event spelling, and the vModelText runtime directive
        // rides in the withDirectives array.
        EmitPrefixed("<input v-model=\"name\" />").Code.ShouldBeCode(
"""
return _withDirectives(_createElementBlock(_openBlock(), "input", _createProps(("onUpdate:modelValue", _withHandler(__event => ((_ctx.name) = __event)))), null, 8 /* PROPS */, ["onUpdate:modelValue"]), new object?[] { new object?[] { _vModelText, _ctx.name } });

""");
    }

    // ---- runtime directives (upstream withDirectives arrays) ----

    [Fact]
    public void VShow_EmitsWithDirectivesArray()
    {
        // Upstream [[_vShow, exp]] emits as nested object?[] arrays (JavaScript array literals have no
        // untyped C# counterpart; docs/DESIGN.md).
        EmitPrefixed("<div v-show=\"visible\">shown</div>").Code.ShouldBeCode(
"""
return _withDirectives(_createElementBlock(_openBlock(), "div", null, "shown", 512 /* NEED_PATCH */), new object?[] { new object?[] { _vShow, _ctx.visible } });

""");
    }

    [Fact]
    public void CustomDirective_EmitsResolveDirectivePreamble()
    {
        EmitPrefixed("<input v-focus />").Code.ShouldBeCode(
"""
var _directive_focus = _resolveDirective("focus");

return _withDirectives(_createElementBlock(_openBlock(), "input", null, null, 512 /* NEED_PATCH */), new object?[] { new object?[] { _directive_focus } });

""");
    }

    // ---- roots and edges ----

    [Fact]
    public void TextRoot_ReturnsStringLiteral()
    {
        // Upstream returns the bare text for a single text root; the runtime normalizes it.
        EmitPrefixed("hello").Code.ShouldBeCode("return \"hello\";\n");
    }

    [Fact]
    public void EmptyTemplate_ReturnsNull()
    {
        EmitPrefixed(string.Empty).Code.ShouldBeCode("return null;\n");
    }

    [Fact]
    public void IndentLevel_PrefixesEveryLine()
    {
        var emitted = Emit("<div>x</div>", new RenderFunctionEmitterOptions { IndentLevel = 2 });
        emitted.Code.ShouldBeCode("        return _createElementBlock(_openBlock(), \"div\", null, \"x\");\n");
    }

    // ---- parse validity: every emitted body is syntactically valid C# ----

    [Theory]
    [InlineData("<div :id=\"dynamicId\" class=\"static\">{{ message }}</div>")]
    [InlineData("<div v-if=\"visible\">A</div><span v-else-if=\"other\">B</span><p v-else>C</p>")]
    [InlineData("<div v-if=\"ok\">A</div>")]
    [InlineData("<li v-for=\"item in items\" :key=\"item.id\">{{ item.label }}</li>")]
    [InlineData("<i v-for=\"(item, index) in items\">{{ index }}</i>")]
    [InlineData("<template v-for=\"row in rows\"><td>{{ row }}</td><td>b</td></template>")]
    [InlineData("<MyButton :kind=\"kind\"><template #header=\"headerProperties\"><b>{{ headerProperties }}</b></template><span>{{ label }}</span></MyButton>")]
    [InlineData("<component :is=\"viewName\"></component>")]
    [InlineData("<slot name=\"header\"><p>fallback {{ hint }}</p></slot>")]
    [InlineData("<div v-once><span>{{ frozen }}</span></div>")]
    [InlineData("<button @click=\"count++\" @submit=\"save\">Go</button>")]
    [InlineData("<button @click=\"save($event)\">x</button>")]
    [InlineData("<button @click.stop=\"save($event)\">x</button>")]
    [InlineData("<input v-model=\"name\" />")]
    [InlineData("<div v-show=\"visible\">shown</div>")]
    [InlineData("<input v-focus />")]
    [InlineData("<div>one</div><span>{{ two }}</span>")]
    [InlineData("<Teleport to=\"body\"><div>{{ tip }}</div></Teleport>")]
    [InlineData("<ul><li>1</li><li>2</li><li>3</li><li>{{ four }}</li></ul>")]
    [InlineData("hello")]
    [InlineData("")]
    public void EmittedBody_ParsesAsValidCSharp(string source)
    {
        // The compile-check the work item requires: the emitted body must be syntactically valid C#
        // when hosted in the render-method shape the generator emits (full semantic binding against the
        // runtime helper surface is the runtime-side integration deliverable).
        var emitted = EmitPrefixed(source);
        var unit =
            "internal static class RenderProbe { internal static object? Render(object _ctx, object?[] _cache) {\n" +
            emitted.Code +
            "} }";

        var tree = CSharpSyntaxTree.ParseText(unit, new CSharpParseOptions(LanguageVersion.Preview));
        tree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == RoslynDiagnosticSeverity.Error)
            .ShouldBeEmpty(customMessage: $"emitted body should parse: {emitted.Code}");
    }

    // ---- determinism and the value-equatable result contract ----

    [Fact]
    public void Emit_IsDeterministic_AndResultIsValueEquatable()
    {
        // The incremental-caching contract: two independent parse+transform+emit runs over the same
        // input produce byte-identical code and value-equal (equally hashed) result records.
        const string source = "<div :id=\"dynamicId\" @click=\"count++\">{{ message }}</div>";
        var first = EmitPrefixed(source);
        var second = EmitPrefixed(source);

        second.Code.ShouldBeCode(first.Code);
        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void HelperSpelling_IsUnderscorePrefixedUpstreamName()
    {
        // Pins the by-name helper contract's spelling rule: `_` + the upstream helperNameMap name
        // (matching TransformContext.HelperString and upstream aliasHelper). A change here is a
        // breaking change to the runtime-side helper surface.
        var emitted = EmitPrefixed("<div>{{ message }}</div>");
        emitted.Code.ShouldContain("_openBlock()");
        emitted.Code.ShouldContain("_createElementBlock(");
        emitted.Code.ShouldContain("_toDisplayString(");
    }

    // ---- harness ----

    private static RenderFunctionEmitterResult EmitPrefixed(string source)
        => Emit(source, new RenderFunctionEmitterOptions());

    private static RenderFunctionEmitterResult Emit(string source, RenderFunctionEmitterOptions options)
    {
        // The generator's template-compilation configuration ([V01.01.06.02] composition root):
        // DOM transforms with PrefixIdentifiers and (for now) empty binding metadata.
        var root = TemplateParser.Parse(source, ParserOptions.CreateHtml());
        var transformOptions = TransformOptions.CreateDom();
        transformOptions.PrefixIdentifiers = true;
        transformOptions.BindingMetadata = BindingMetadata.Empty;
        var result = Transformer.Transform(root, transformOptions);
        return RenderFunctionEmitter.Emit(result, options);
    }
}

/// <summary>
/// Compares emitted render code against a snapshot expectation with the expectation's line endings
/// normalized to LF: snapshot literals inherit the checkout's line endings (nothing pins them), while
/// the emitter's documented contract is LF — normalizing keeps the pins checkout-independent instead
/// of failing on autocrlf working trees.
/// </summary>
internal static class RenderCodeAssertions
{
    /// <summary>Asserts <paramref name="actual"/> equals <paramref name="expected"/> after LF-normalizing the expectation.</summary>
    /// <param name="actual">The emitted render code (LF by contract).</param>
    /// <param name="expected">The snapshot expectation, in the checkout's line endings.</param>
    public static void ShouldBeCode(this string actual, string expected)
        => actual.ShouldBe(expected.Replace("\r\n", "\n"));
}
