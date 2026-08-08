using System.Linq;

using Microsoft.CodeAnalysis.CSharp;

using Shouldly;

using Xunit;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Pins the frame-based statement output produced for <c>[SFC-CG-1]</c> through
/// <c>[SFC-CG-7]</c>. These tests intentionally assert the adopted runtime vocabulary and reject
/// the retired by-name helper surface while leaving local-name allocation free to evolve.
/// </summary>
public class RenderFunctionEmitterTests
{
    [Fact]
    public void Element_WithMixedProperties_EmitsDirectNodeBindingsAndRawPatchFlags()
    {
        RenderFunctionEmitterResult emitted = EmitPrefixed(
            "<div :id=\"dynamicId\" class=\"static\">{{ message }}</div>");

        emitted.Code.ShouldContain("frame.OpenBlock();");
        emitted.Code.ShouldContain("new global::System.Collections.Generic.Dictionary<string, object?>");
        emitted.Code.ShouldContain("[\"id\"] = component.dynamicId");
        emitted.Code.ShouldContain("ElementBinding.Attribute(");
        emitted.Code.ShouldContain("new global::Assimalign.Viu.Components.ElementNode(");
        emitted.Code.ShouldContain("(global::Assimalign.Viu.Components.PatchFlags)(9) /* TEXT, PROPS */");
        emitted.Code.ShouldContain("frame.CloseBlock()");
        emitted.Code.ShouldContain("frame.Track(");
        emitted.CacheSlotCount.ShouldBe(0);
    }

    [Fact]
    public void Conditional_EmitsOrderedStatementsAndCommentFallback()
    {
        string code = EmitPrefixed("<div v-if=\"visible\">A</div>").Code;

        code.ShouldContain("if (component.visible)");
        code.ShouldContain("new global::Assimalign.Viu.Components.ElementNode(");
        code.ShouldContain("new global::Assimalign.Viu.Components.CommentNode(\"v-if\")");
        code.ShouldContain("conditionalNode");
    }

    [Fact]
    public void RenderList_EmitsDirectForeachAndFragmentConstruction()
    {
        string code = EmitPrefixed(
            "<li v-for=\"item in items\" :key=\"item.id\">{{ item.label }}</li>").Code;

        code.ShouldContain("var listSource");
        code.ShouldContain("= component.items;");
        code.ShouldContain("foreach (var listItem");
        code.ShouldContain("var item = listItem");
        code.ShouldContain("new global::Assimalign.Viu.Components.FragmentNode(");
        code.ShouldContain("(global::Assimalign.Viu.Components.PatchFlags)(128) /* KEYED_FRAGMENT */");
    }

    [Fact]
    public void ComponentSlots_EmitInvocationDictionariesClosuresAndStability()
    {
        RenderFunctionEmitterResult emitted = EmitPrefixed(
            "<MyButton :kind=\"kind\"><template #header=\"headerProperties\"><b>{{ headerProperties }}</b></template>" +
            "<span>{{ label }}</span></MyButton>");

        emitted.Code.ShouldContain("ComponentReference.ForName(\"MyButton\")");
        emitted.Code.ShouldContain("Dictionary<string, global::Assimalign.Viu.Components.ComponentSlot>");
        emitted.Code.ShouldContain("global::Assimalign.Viu.Components.ComponentSlot");
        emitted.Code.ShouldContain("new global::Assimalign.Viu.Components.ComponentInvocation(");
        emitted.Code.ShouldContain("slotStability: (global::Assimalign.Viu.Components.SlotStability)1");
        emitted.Code.ShouldContain("new global::Assimalign.Viu.Components.ComponentNode(");
    }

    [Fact]
    public void SlotOutlet_ReadsContractBindingsAndEmitsFallbackStatements()
    {
        string code = EmitPrefixed("<slot name=\"header\"><p>fallback {{ hint }}</p></slot>").Code;

        code.ShouldContain("component.Context!.Bindings.Slots.TryGetValue(");
        code.ShouldContain("slot2(slotArguments1)");
        code.ShouldContain("DisplayStringFormatter.ToDisplayString(component.hint)");
        code.ShouldContain("slotNode");
        code.ShouldContain("frame.Track(slotNode");
    }

    [Fact]
    public void DynamicAndStructuralComponents_UseAdoptedRuntimeTypes()
    {
        string dynamicCode = EmitPrefixed("<component :is=\"viewName\"></component>").Code;
        string teleportCode = EmitPrefixed("<Teleport to=\"body\"><div>{{ tip }}</div></Teleport>").Code;

        dynamicCode.ShouldContain("global::Assimalign.Viu.DynamicComponents.DynamicComponent(");
        dynamicCode.ShouldContain("component.viewName");
        teleportCode.ShouldContain("new global::Assimalign.Viu.Components.TeleportNode(");
        teleportCode.ShouldContain("targetIdentifier");
    }

    [Fact]
    public void CachedSubtree_UsesFrameOwnedSlotsAndBalancedTracking()
    {
        RenderFunctionEmitterResult emitted = EmitPrefixed("<div v-once><span>{{ frozen }}</span></div>");

        emitted.Code.ShouldContain("frame.SetBlockTracking(-1)");
        emitted.Code.ShouldContain("finally");
        emitted.Code.ShouldContain("frame.SetBlockTracking(1)");
        emitted.Code.ShouldContain("frame.GetOrAddCache<global::Assimalign.Viu.Components.VirtualNode?>");
        emitted.CacheSlotCount.ShouldBe(1);
    }

    [Fact]
    public void MemoizedSubtree_UsesTheFrameMemoChannel()
    {
        RenderFunctionEmitterResult emitted = EmitPrefixed("<div v-memo=\"[message]\">{{ message }}</div>");

        emitted.Code.ShouldContain("frame.Memo(0, ");
        emitted.Code.ShouldContain("component.message");
        emitted.Code.ShouldContain("renderMemoNode");
        emitted.Code.ShouldNotContain("_cache");
        emitted.Code.ShouldNotContain("_withMemo");
        emitted.Code.ShouldNotContain(
            "DisplayStringFormatter.ToDisplayString(global::Assimalign.Viu.DisplayStringFormatter.ToDisplayString(");
        emitted.CacheSlotCount.ShouldBe(1);
    }

    [Fact]
    public void MemoizedRenderList_UsesTypedPerItemFrameCacheEntries()
    {
        RenderFunctionEmitterResult emitted = EmitPrefixed(
            "<div v-for=\"item in items\" v-memo=\"[item.id]\" :key=\"item.id\">{{ item.label }}</div>");

        emitted.Code.ShouldContain("previousMemoEntries");
        emitted.Code.ShouldContain("global::System.Linq.Enumerable.SequenceEqual(");
        emitted.Code.ShouldContain("frame.SetCache(0, currentMemoEntries");
        emitted.Code.ShouldContain("memoizedItem");
        emitted.Code.ShouldNotContain("_isMemoSame");
        emitted.CacheSlotCount.ShouldBe(1);
    }

    [Fact]
    public void CachedPatchFlag_ParenthesizesTheNegativeRawInteger()
    {
        RootNode root = TemplateParser.Parse("<div><span>static</span></div>", ParserOptions.CreateHtml());
        TransformOptions options = TransformOptions.CreateDom();
        options.HoistStatic = true;
        string code = RenderFunctionEmitter.Emit(Transformer.Transform(root, options)).Code;

        code.ShouldContain("(global::Assimalign.Viu.Components.PatchFlags)(-1) /* CACHED */");
        code.ShouldNotContain("(global::Assimalign.Viu.Components.PatchFlags)-1");
    }

    [Fact]
    public void CssModuleAccessor_RemainsAStaticGeneratedTypeReference()
    {
        RootNode root = TemplateParser.Parse("<div :class=\"theme.active\"></div>", ParserOptions.CreateHtml());
        TransformOptions options = TransformOptions.CreateDom();
        options.PrefixIdentifiers = true;
        options.BindingMetadata = BindingMetadata.Empty;
        options.CssModules = new CssModuleAccessors(
            new[] { new CssModuleAccessor("theme", "theme", "Theme", new[] { "active" }) },
            reportsUnknownMembers: true);

        string code = RenderFunctionEmitter.Emit(Transformer.Transform(root, options)).Code;

        code.ShouldContain("Theme.active");
        code.ShouldNotContain("component.Theme.active");
    }

    [Fact]
    public void EventHandlers_UseOnlyTheNarrowGeneratedAdapter()
    {
        string code = EmitPrefixed("<button @click=\"count++\" @submit=\"save\">Go</button>").Code;

        code.ShouldContain("global::Assimalign.Viu.Generated.RenderGlue.Handler(");
        code.ShouldContain("eventValue => (component.count++)");
        code.ShouldContain("component.save");
        code.ShouldContain("ElementBinding.Event(");
    }

    [Fact]
    public void EventModifiers_UseTypedBrowserEventGuards()
    {
        string code = EmitPrefixed("<button @click.stop=\"save($event)\">x</button>").Code;

        code.ShouldContain("global::Assimalign.Viu.Browser.BrowserEvents.WithModifiers(");
        code.ShouldContain("component.save(eventValue)");
        code.ShouldContain("[\"stop\"]");
    }

    [Fact]
    public void NativeModel_EmitsTypedCarrierDirectiveAndModifiers()
    {
        string code = EmitPrefixed("<input v-model.trim.number=\"name\" />").Code;

        code.ShouldContain("new global::Assimalign.Viu.Browser.ViuModelBinding(");
        code.ShouldContain("component.name");
        code.ShouldContain("new string[] { \"trim\", \"number\" }");
        code.ShouldContain("DirectiveInvocation(typeof(global::Assimalign.Viu.Browser.VModelText)");
    }

    [Fact]
    public void Directives_EmitCompileTimeTypeTokens()
    {
        string showCode = EmitPrefixed("<div v-show=\"visible\">shown</div>").Code;
        string customCode = EmitPrefixed("<input v-focus />").Code;

        showCode.ShouldContain("DirectiveInvocation(typeof(global::Assimalign.Viu.Browser.VShow)");
        customCode.ShouldContain("DirectiveInvocation(typeof(Focus)");
        customCode.ShouldNotContain("resolveDirective");
    }

    [Fact]
    public void TextAndEmptyRoots_ReturnClosedNodeValues()
    {
        string textCode = EmitPrefixed("hello").Code;
        string emptyCode = EmitPrefixed(string.Empty).Code;

        textCode.ShouldContain("new global::Assimalign.Viu.Components.TextNode(\"hello\")");
        textCode.ShouldEndWith("return node0;\n");
        emptyCode.ShouldBeCode("return null;\n");
    }

    [Fact]
    public void IndentLevel_PrefixesEveryGeneratedLine()
    {
        RenderFunctionEmitterResult emitted = Emit(
            "hello",
            new RenderFunctionEmitterOptions { IndentLevel = 2 });

        emitted.Code.Split('\n')
            .Where(line => line.Length > 0)
            .ShouldAllBe(line => line.StartsWith("        "));
    }

    [Theory]
    [InlineData("<div :id=\"dynamicId\" class=\"static\">{{ message }}</div>")]
    [InlineData("<div v-if=\"visible\">A</div><span v-else-if=\"other\">B</span><p v-else>C</p>")]
    [InlineData("<li v-for=\"item in items\" :key=\"item.id\">{{ item.label }}</li>")]
    [InlineData("<template v-for=\"row in rows\"><td>{{ row }}</td><td>b</td></template>")]
    [InlineData("<MyButton :kind=\"kind\"><span>{{ label }}</span></MyButton>")]
    [InlineData("<component :is=\"viewName\"></component>")]
    [InlineData("<slot name=\"header\"><p>fallback {{ hint }}</p></slot>")]
    [InlineData("<div v-once><span>{{ frozen }}</span></div>")]
    [InlineData("<button @click.stop=\"save($event)\">x</button>")]
    [InlineData("<input v-model.trim.number=\"name\" />")]
    [InlineData("<div v-show=\"visible\">shown</div>")]
    [InlineData("<input v-focus />")]
    [InlineData("<Teleport to=\"body\"><div>{{ tip }}</div></Teleport>")]
    [InlineData("hello")]
    [InlineData("")]
    public void EmittedBody_ParsesAsValidCSharp(string source)
    {
        RenderFunctionEmitterResult emitted = EmitPrefixed(source);
        string unit =
            "internal sealed class RenderProbe {\n" +
            "    private static object? Render(RenderProbe component, object frame) {\n" +
            emitted.Code +
            "    }\n" +
            "}";

        var tree = CSharpSyntaxTree.ParseText(unit, new CSharpParseOptions(LanguageVersion.Preview));
        tree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == RoslynDiagnosticSeverity.Error)
            .ShouldBeEmpty(customMessage: $"emitted body should parse: {emitted.Code}");
    }

    [Fact]
    public void Emit_IsDeterministic_AndResultIsValueEquatable()
    {
        const string source = "<div :id=\"dynamicId\" @click=\"count++\">{{ message }}</div>";
        RenderFunctionEmitterResult first = EmitPrefixed(source);
        RenderFunctionEmitterResult second = EmitPrefixed(source);

        second.Code.ShouldBeCode(first.Code);
        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Theory]
    [InlineData("_cache")]
    [InlineData("_cacheValue")]
    [InlineData("_cached")]
    [InlineData("_cachedValue")]
    [InlineData("_cached.key")]
    [InlineData("_memo")]
    [InlineData("_memoValue")]
    [InlineData("_item")]
    [InlineData("_itemValue")]
    [InlineData("_unref")]
    [InlineData("_camelize")]
    [InlineData("_toDisplayString")]
    [InlineData("_toHandlerKey")]
    [InlineData("_ctx")]
    [InlineData("_ctxValue")]
    public void AuthoredUnderscorePrefixedMember_IsNotRewrittenAsCompilerOwnedIdentifier(string expression)
    {
        string code = EmitPrefixed("<div>{{ " + expression + " }}</div>").Code;

        code.ShouldContain("component." + expression);
    }

    [Fact]
    public void Output_ContainsNoRetiredHelperOrAmbientStateSurface()
    {
        string code = EmitPrefixed(
            "<MyButton @save=\"save\"><div v-if=\"visible\" v-show=\"visible\">{{ message }}</div></MyButton>").Code;

        code.ShouldNotContain("using static");
        code.ShouldNotContain("Render" + "Helpers");
        code.ShouldNotContain("Block" + "Token");
        code.ShouldNotContain("_openBlock");
        code.ShouldNotContain("_createElement");
        code.ShouldNotContain("_resolveComponent");
        code.ShouldNotContain("_withDirectives");
        code.ShouldNotContain("_toDisplayString");
        code.ShouldNotContain("IsGeneratedEventName(");
        code.ShouldNotContain("IsGeneratedHostPropertyName(");
        code.ShouldNotContain("ToEventName(");
    }

    private static RenderFunctionEmitterResult EmitPrefixed(string source) =>
        Emit(source, new RenderFunctionEmitterOptions());

    private static RenderFunctionEmitterResult Emit(
        string source,
        RenderFunctionEmitterOptions options)
    {
        RootNode root = TemplateParser.Parse(source, ParserOptions.CreateHtml());
        TransformOptions transformOptions = TransformOptions.CreateDom();
        transformOptions.PrefixIdentifiers = true;
        transformOptions.BindingMetadata = BindingMetadata.Empty;
        TransformResult result = Transformer.Transform(root, transformOptions);
        return RenderFunctionEmitter.Emit(result, options);
    }
}

/// <summary>Provides checkout-independent LF comparisons for emitted render code.</summary>
internal static class RenderCodeAssertions
{
    /// <param name="actual">The emitted render code.</param>
    extension(string actual)
    {
        /// <summary>Asserts exact code after normalizing the expectation to LF.</summary>
        /// <param name="expected">The expected emitted text.</param>
        public void ShouldBeCode(string expected) =>
            actual.ShouldBe(expected.Replace("\r\n", "\n"));
    }
}
