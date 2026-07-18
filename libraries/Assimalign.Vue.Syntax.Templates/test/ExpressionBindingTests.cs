using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Templates;

// Ported from vuejs/core packages/compiler-core/__tests__/transforms/transformExpression.spec.ts: identifier
// prefixing, binding-metadata classification, ref unwrapping, v-for/v-slot scope, and expression validation.
// Vuecs divergences (C# .Value unwrapping in read and write positions, _ctx member access, the strict
// unresolved-identifier diagnostic) are pinned here and documented in
// libraries/Assimalign.Vue.Syntax.Templates/docs/DESIGN.md.
public class ExpressionBindingTests
{
    // ---- interpolation identifier rewriting ----

    [Fact]
    public void ProcessExpression_ComponentDataMember_PrefixesWithContext()
    {
        // Unknown-source members resolve to _ctx.<name>, mirroring Vue's default _ctx prefix.
        SingleInterpolation("{{ message }}", Bindings(("message", BindingType.Data)))
            .ShouldBe("_ctx.message");
    }

    [Fact]
    public void ProcessExpression_SetupReference_InsertsValueAccessor()
    {
        // upstream SETUP_REF -> `foo.value`; Vuecs uses the C# settable Ref<T>.Value.
        SingleInterpolation("{{ count }}", Bindings(("count", BindingType.SetupReference)))
            .ShouldBe("count.Value");
    }

    [Fact]
    public void ProcessExpression_SetupConstant_StaysBare()
    {
        // upstream SETUP_CONST in inline mode returns the raw name (a render-closure local); never unwrapped.
        SingleInterpolation("{{ total }}", Bindings(("total", BindingType.SetupConstant)))
            .ShouldBe("total");
    }

    [Fact]
    public void ProcessExpression_SetupMaybeReference_GuardsReadWithUnref()
    {
        // upstream SETUP_MAYBE_REF read -> `unref(x)`.
        SingleInterpolation("{{ maybe }}", Bindings(("maybe", BindingType.SetupMaybeReference)))
            .ShouldBe("_unref(maybe)");
    }

    [Fact]
    public void ProcessExpression_SetupMaybeReference_RegistersUnrefHelper()
    {
        var result = TransformPrefixed("<div>{{ maybe }}</div>", Bindings(("maybe", BindingType.SetupMaybeReference)), out _);
        result.UsesHelper("unref").ShouldBeTrue();
    }

    [Fact]
    public void ProcessExpression_PropertyBinding_PrefixesWithContext()
    {
        SingleInterpolation("{{ title }}", Bindings(("title", BindingType.Property)))
            .ShouldBe("_ctx.title");
    }

    [Fact]
    public void ProcessExpression_AliasedProperty_ResolvesRealPropertyName()
    {
        var metadata = new BindingMetadata(
            new Dictionary<string, BindingType> { ["alias"] = BindingType.PropertyAliased },
            propertyAliases: new Dictionary<string, string> { ["alias"] = "realProperty" });

        SingleInterpolation("{{ alias }}", metadata).ShouldBe("_ctx.realProperty");
    }

    [Fact]
    public void ProcessExpression_MemberExpression_RewritesOnlyTheRoot()
    {
        // `user.name`: only the root identifier is a reference; `name` is a member and stays literal.
        SingleInterpolation("{{ user.name }}", Bindings(("user", BindingType.Data)))
            .ShouldBe("_ctx.user.name");
    }

    [Fact]
    public void ProcessExpression_ReferenceInMemberReceiver_UnwrapsThenAccessesMember()
    {
        // A ref used as a member-access receiver unwraps first: `user.Name` -> `user.Value.Name`.
        SingleInterpolation("{{ user.Name }}", Bindings(("user", BindingType.SetupReference)))
            .ShouldBe("user.Value.Name");
    }

    [Fact]
    public void ProcessExpression_ReferenceInElementAccess_UnwrapsReceiverOnly()
    {
        // A ref used as an indexer receiver unwraps; the literal index is untouched.
        SingleInterpolation("{{ items[0] }}", Bindings(("items", BindingType.SetupReference)))
            .ShouldBe("items.Value[0]");
    }

    [Fact]
    public void ProcessExpression_LambdaParameter_IsExcludedFromRewriting()
    {
        // A lambda parameter is a local of the expression: `x` stays bare, `list` resolves through _ctx.
        SingleInterpolation("{{ list.Where(x => x.Active) }}", Bindings(("list", BindingType.Data)))
            .ShouldBe("_ctx.list.Where(x => x.Active)");
    }

    [Fact]
    public void ProcessExpression_AllowedGlobal_IsNeverRewritten()
    {
        // Math is an allowed global; a and b are members. Mirrors upstream isGloballyAllowed skipping.
        SingleInterpolation("{{ Math.Max(a, b) }}", Bindings(("a", BindingType.Data), ("b", BindingType.Data)))
            .ShouldBe("Math.Max(_ctx.a, _ctx.b)");
    }

    [Fact]
    public void ProcessExpression_BinaryExpression_RewritesEachOperand()
    {
        SingleInterpolation(
            "{{ count + step }}",
            Bindings(("count", BindingType.SetupReference), ("step", BindingType.Data)))
            .ShouldBe("count.Value + _ctx.step");
    }

    [Fact]
    public void ProcessExpression_PrefixingDisabled_LeavesExpressionOpaque()
    {
        // Without PrefixIdentifiers the pipeline matches Vue's browser build: expression bodies are untouched.
        var interpolation = TransformTestHelpers.Transform("{{ count }}").SingleChild().ShouldBeOfType<InterpolationNode>();
        Flatten(interpolation.Content).ShouldBe("count");
    }

    // ---- ref unwrapping in write positions (v-on inline handlers) ----

    [Fact]
    public void ProcessExpression_IncrementOnReference_UnwrapsWriteTarget()
    {
        // upstream: `count++` on a ref becomes `count.value++` inside the inline handler.
        HandlerBody("<button @click=\"count++\"></button>", Bindings(("count", BindingType.SetupReference)))
            .ShouldBe("__event => (count.Value++)");
    }

    [Fact]
    public void ProcessExpression_CompoundAssignmentOnReference_UnwrapsWriteTarget()
    {
        HandlerBody("<button @click=\"count += 1\"></button>", Bindings(("count", BindingType.SetupReference)))
            .ShouldBe("__event => (count.Value += 1)");
    }

    [Fact]
    public void ProcessExpression_SimpleAssignmentOnReference_UnwrapsWriteTarget()
    {
        HandlerBody("<button @click=\"count = 5\"></button>", Bindings(("count", BindingType.SetupReference)))
            .ShouldBe("__event => (count.Value = 5)");
    }

    [Fact]
    public void ProcessExpression_NonReferenceAssignment_IsNeverUnwrapped()
    {
        // A data member assigned in a handler resolves through _ctx and gains no .Value.
        HandlerBody("<button @click=\"open = true\"></button>", Bindings(("open", BindingType.Data)))
            .ShouldBe("__event => (_ctx.open = true)");
    }

    [Fact]
    public void ProcessExpression_MethodHandler_ResolvesWithoutInlineWrapper()
    {
        // A bare member handler is a method reference, not an inline statement: no $event wrapper.
        HandlerValue("<button @click=\"submit\"></button>", Bindings(("submit", BindingType.Options)))
            .ShouldBe("_ctx.submit");
    }

    // ---- v-bind and v-model expressions ----

    [Fact]
    public void ProcessExpression_VBindReference_UnwrapsBoundValue()
    {
        var result = TransformPrefixed("<div :id=\"itemId\"></div>", Bindings(("itemId", BindingType.SetupReference)), out _);
        Flatten(PropertyValue(result, "id")).ShouldBe("itemId.Value");
    }

    [Fact]
    public void ProcessExpression_VModelReference_UnwrapsModelValue()
    {
        // The v-model expression is rewritten by transformExpression before the model directive consumes it.
        var result = TransformPrefixed("<input v-model=\"name\"/>", Bindings(("name", BindingType.SetupReference)), out _);
        FlattenProps(result).ShouldContain("name.Value");
    }

    [Fact]
    public void ProcessExpression_DynamicArgument_IsRewritten()
    {
        // A dynamic v-bind argument (`:[key]`) is a non-static expression and is rewritten too.
        var result = TransformPrefixed("<div :[key]=\"value\"></div>", Bindings(("key", BindingType.Data), ("value", BindingType.Data)), out _);
        var props = FlattenProps(result);
        props.ShouldContain("_ctx.key");
        props.ShouldContain("_ctx.value");
    }

    // ---- v-for / v-slot scope and shadowing ----

    [Fact]
    public void ProcessExpression_VForAlias_ShadowsComponentMember()
    {
        // `item` is a component member but the v-for alias shadows it inside the loop; the sibling does not.
        var interpolations = Interpolations(
            "<div><span v-for=\"item in list\">{{ item }}</span><b>{{ item }}</b></div>",
            Bindings(("item", BindingType.Data), ("list", BindingType.Data)));

        interpolations.ShouldBe(new[] { "item", "_ctx.item" });
    }

    [Fact]
    public void ProcessExpression_VForKeyAndIndexAliases_AreExcludedFromPrefixing()
    {
        // (value, key, index) aliases all enter scope and stay bare.
        SingleInterpolation(
            "<div v-for=\"(value, key, index) in map\">{{ value }}-{{ key }}-{{ index }}</div>",
            Bindings(("map", BindingType.Data)),
            join: true)
            .ShouldBe("value-key-index");
    }

    [Fact]
    public void ProcessExpression_NestedVForSameAlias_ShadowsAndRestoresOuter()
    {
        // Inner `a` shadows outer `a`; after the inner loop pops, the outer alias is still in scope (ref-count).
        var interpolations = Interpolations(
            "<div v-for=\"a in xs\"><i v-for=\"a in a\">{{ a }}</i>{{ a }}</div>",
            Bindings(("a", BindingType.Data), ("xs", BindingType.Data)));

        interpolations.ShouldBe(new[] { "a", "a" });
    }

    [Fact]
    public void ProcessExpression_VForSource_IsRewrittenInOuterScope()
    {
        // The iterated source is evaluated before the aliases exist, so a ref source unwraps normally.
        var result = TransformPrefixed(
            "<div v-for=\"item in items\">{{ item }}</div>",
            Bindings(("items", BindingType.SetupReference)),
            out _);

        FlattenTree(result.RootCodegen()).ShouldContain("items.Value");
    }

    [Fact]
    public void ProcessExpression_VSlotProperties_AreInScopeForSlotChildren()
    {
        // `slotProperties` is introduced by v-slot and is a template-local inside the slot content.
        SingleInterpolation(
            "<Comp v-slot=\"slotProperties\"><span>{{ slotProperties }}</span></Comp>",
            BindingMetadata.Empty)
            .ShouldBe("slotProperties");
    }

    [Fact]
    public void ProcessExpression_VSlotDestructuredProperties_EnterScope()
    {
        // A destructured slot prop contributes each name to scope (lenient extraction).
        SingleInterpolation(
            "<Comp v-slot=\"{ row }\"><span>{{ row }}</span></Comp>",
            Bindings(("row", BindingType.Data)))
            .ShouldBe("row");
    }

    // ---- expression validation and diagnostics ----

    [Fact]
    public void ProcessExpression_MalformedExpression_ReportsInvalidExpression()
    {
        TransformPrefixed("<div>{{ a + }}</div>", Bindings(("a", BindingType.Data)), out var errors);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(CompilerErrorCode.XInvalidExpression);
        error.Message.ShouldStartWith("Error parsing JavaScript expression:");
    }

    [Fact]
    public void ProcessExpression_MalformedExpression_RemapsLocationIntoTemplate()
    {
        const string source = "<div>{{ a + }}</div>";

        // The content's template offset, taken from an opaque (non-prefixed) parse of the same input.
        var opaque = TransformTestHelpers.Transform(source);
        var vnode = opaque.RootCodegen().ShouldBeOfType<VNodeCall>();
        var interpolation = vnode.Children.ShouldBeOfType<InterpolationNode>();
        var expressionStart = interpolation.Content.Location.Start.Offset;

        TransformPrefixed(source, Bindings(("a", BindingType.Data)), out var errors);

        // The reported offset is in template coordinates (>= the expression start), not sub-expression-relative.
        errors.ShouldHaveSingleItem().Location.Start.Offset.ShouldBeGreaterThanOrEqualTo(expressionStart);
    }

    [Fact]
    public void ProcessExpression_MalformedExpression_IsRecoverableNotThrown()
    {
        // Parsing is recoverable: a diagnostic is reported and the transform still completes.
        Should.NotThrow(() => TransformPrefixed("<div>{{ )( }}</div>", BindingMetadata.Empty, out _));
    }

    [Fact]
    public void ProcessExpression_OpaqueModeMalformedExpression_IsNotValidated()
    {
        // Without prefixing there is no Roslyn validation, matching Vue's browser build.
        TransformTestHelpers.Transform("<div>{{ a + }}</div>", out var errors);
        errors.ShouldBeEmpty();
    }

    // ---- strict unresolved-identifier diagnostic (Vuecs-specific) ----

    [Fact]
    public void ProcessExpression_UnknownIdentifierUnderStrictMetadata_ReportsUnresolved()
    {
        var metadata = new BindingMetadata(
            new Dictionary<string, BindingType> { ["known"] = BindingType.Data },
            reportsUnresolvedIdentifiers: true);

        TransformPrefixed("<div>{{ mystery }}</div>", metadata, out var errors);

        errors.ShouldHaveSingleItem().Code.ShouldBe(CompilerErrorCode.XVuecsUnresolvedIdentifier);
    }

    [Fact]
    public void ProcessExpression_UnknownIdentifierUnderPermissiveMetadata_FallsBackToContext()
    {
        // Default (non-strict) metadata never errors: unknown identifiers use the _ctx fallback like Vue.
        var result = TransformPrefixed("<div>{{ mystery }}</div>", BindingMetadata.Empty, out var errors);
        errors.ShouldBeEmpty();
        FlattenTree(result.RootCodegen()).ShouldContain("_ctx.mystery");
    }

    [Fact]
    public void ProcessExpression_UnknownIdentifierUnderStrictMetadata_StillEmitsContextAccess()
    {
        // The diagnostic is recoverable: a _ctx access is still emitted so later stages see a valid tree.
        var metadata = new BindingMetadata(
            new Dictionary<string, BindingType>(),
            reportsUnresolvedIdentifiers: true);

        var result = TransformPrefixed("<div>{{ mystery }}</div>", metadata, out _);
        FlattenTree(result.RootCodegen()).ShouldContain("_ctx.mystery");
    }

    // ---- incremental-caching contract ----

    [Fact]
    public void ProcessExpression_EqualInput_ProducesValueEqualRewrittenExpression()
    {
        var metadata = Bindings(("count", BindingType.SetupReference), ("step", BindingType.Data));

        var first = FirstInterpolationContent("<div>{{ count + step }}</div>", metadata);
        var second = FirstInterpolationContent("<div>{{ count + step }}</div>", metadata);

        first.ShouldBe(second);
    }

    // ---- structural-directive expression sites (vIf.ts processIf, vSlot.ts trackVForSlotScopes) ----

    [Fact]
    public void ProcessExpression_VIfCondition_RewritesUnderPrefixing()
    {
        // Upstream processIf runs processExpression on dir.exp under prefixIdentifiers because vIf is a
        // structural transform applied before transformExpression (vuejs/core v3.5 transforms/vIf.ts).
        var result = TransformPrefixed(
            "<span v-if=\"visible\">a</span><span v-else-if=\"other\">b</span>",
            Bindings(("visible", BindingType.SetupReference), ("other", BindingType.Data)),
            out var errors);

        errors.ShouldBeEmpty();
        var ifNode = result.Children[0].ShouldBeOfType<IfNode>();
        Flatten(ifNode.Branches[0].Condition).ShouldBe("visible.Value");
        Flatten(ifNode.Branches[1].Condition).ShouldBe("_ctx.other");
    }

    [Fact]
    public void ProcessExpression_TemplateVForSlot_AliasStaysLocalAndSourceRewrites()
    {
        // The structural-directive factory skips template-with-v-slot, so trackVForSlotScopes registers
        // the v-for aliases and buildSlots processes the source (vuejs/core v3.5 transforms/vSlot.ts).
        var result = TransformPrefixed(
            "<Comp><template v-for=\"item in items\" v-slot:row><span>{{ item }}</span></template></Comp>",
            Bindings(("items", BindingType.Data)),
            out var errors);

        errors.ShouldBeEmpty();
        var flattened = FlattenTree(result.RootCodegen());
        flattened.ShouldContain("_ctx.items");
        flattened.ShouldNotContain("_ctx.item)");
        SingleInterpolation(
            "<Comp><template v-for=\"item in items\" v-slot:row><span>{{ item }}</span></template></Comp>",
            Bindings(("items", BindingType.Data)))
            .ShouldBe("item");
    }

    // ---- write-position unwrapping (upstream rewriteIdentifier assignment/update handling) ----

    [Fact]
    public void ProcessExpression_MaybeReferenceWrite_UnwrapsToValue()
    {
        // upstream SETUP_MAYBE_REF in assignment position -> `.value`: a write to a const binding is
        // only legal when it is a ref (transformExpression.ts rewriteIdentifier).
        SingleInterpolation("{{ maybe = 1 }}", Bindings(("maybe", BindingType.SetupMaybeReference)))
            .ShouldBe("maybe.Value = 1");
    }

    [Fact]
    public void ProcessExpression_SetupLetRead_GuardsWithUnref()
    {
        // upstream SETUP_LET read -> `unref(x)`; the guarded write stays bare (documented divergence,
        // docs/DESIGN.md).
        SingleInterpolation("{{ letBinding }}", Bindings(("letBinding", BindingType.SetupLet)))
            .ShouldBe("_unref(letBinding)");
    }

    [Fact]
    public void ProcessExpression_SetupLetWrite_StaysBare()
    {
        SingleInterpolation("{{ letBinding = 1 }}", Bindings(("letBinding", BindingType.SetupLet)))
            .ShouldBe("letBinding = 1");
    }

    // ---- statement-mode locals, object members, and nameof ----

    [Fact]
    public void ProcessExpression_StatementHandlerLocal_IsNotPrefixed()
    {
        // A local declared in a multi-statement handler is a scope identifier, mirroring upstream
        // walkIdentifiers' variable-declaration handling.
        var result = TransformPrefixed(
            "<button @click=\"var next = count + 1; total = next;\"></button>",
            Bindings(("count", BindingType.SetupReference), ("total", BindingType.Data)),
            out var errors);

        errors.ShouldBeEmpty();
        var flattened = FlattenTree(result.RootCodegen());
        flattened.ShouldContain("var next = count.Value + 1");
        flattened.ShouldContain("_ctx.total = next");
        flattened.ShouldNotContain("_ctx.next");
    }

    [Fact]
    public void ProcessExpression_ObjectInitializerMemberNames_AreNotRewritten()
    {
        // `new Point { X = x }`: X names a member of the constructed type; only x is a reference.
        var result = TransformPrefixed(
            "<div :data-point=\"new Point { X = x, Y = y }\"></div>",
            Bindings(("x", BindingType.Data), ("y", BindingType.Data)),
            out var errors);

        errors.ShouldBeEmpty();
        var flattened = FlattenTree(result.RootCodegen());
        flattened.ShouldContain("X = _ctx.x");
        flattened.ShouldContain("Y = _ctx.y");
        flattened.ShouldNotContain("_ctx.X", Case.Sensitive);
    }

    [Fact]
    public void ProcessExpression_NameOf_IsNotRewritten()
    {
        // The nameof operand is a name, not a value read; rewriting it would change the produced string.
        var result = TransformPrefixed(
            "<div :title=\"nameof(count)\"></div>",
            Bindings(("count", BindingType.SetupReference)),
            out var errors);

        errors.ShouldBeEmpty();
        var flattened = FlattenTree(result.RootCodegen());
        flattened.ShouldContain("nameof(count)");
        flattened.ShouldNotContain("_ctx.nameof");
        flattened.ShouldNotContain("count.Value");
    }

    // ---- the Vuecs event identifier (docs/DESIGN.md: template $event <-> C# __event) ----

    [Fact]
    public void ProcessExpression_EventVariable_SubstitutesLegalIdentifierUnderPrefixing()
    {
        var result = TransformPrefixed(
            "<button @click=\"save($event)\"></button>",
            Bindings(("save", BindingType.SetupConstant)),
            out var errors);

        errors.ShouldBeEmpty();
        var flattened = FlattenTree(result.RootCodegen());
        flattened.ShouldContain("__event => ");
        flattened.ShouldContain("save(__event)");
        flattened.ShouldNotContain("$event");
    }

    // ---- helpers ----

    private static BindingMetadata Bindings(params (string Name, BindingType Type)[] entries)
    {
        var map = new Dictionary<string, BindingType>();
        foreach (var (name, type) in entries)
        {
            map[name] = type;
        }

        return new BindingMetadata(map, isScriptSetup: true);
    }

    private static TransformResult TransformPrefixed(string source, BindingMetadata metadata, out List<CompilerError> errors)
    {
        var collected = new List<CompilerError>();
        var options = TransformOptions.CreateDom();
        options.PrefixIdentifiers = true;
        options.BindingMetadata = metadata;
        options.OnError = collected.Add;
        var result = TransformTestHelpers.Transform(source, options);
        errors = collected;
        return result;
    }

    private static List<string> Interpolations(string source, BindingMetadata metadata)
    {
        var collected = new List<string>();
        var options = TransformOptions.CreateDom();
        options.PrefixIdentifiers = true;
        options.BindingMetadata = metadata;
        options.NodeTransforms = new NodeTransform[]
        {
            (node, _) =>
            {
                if (node is InterpolationNode interpolation)
                {
                    collected.Add(Flatten(interpolation.Content));
                }

                return null;
            },
        };
        TransformTestHelpers.Transform(source, options);
        return collected;
    }

    private static string SingleInterpolation(string source, BindingMetadata metadata, bool join = false)
    {
        var interpolations = Interpolations(source, metadata);
        return join ? string.Join("-", interpolations) : interpolations.Single();
    }

    private static ExpressionNode FirstInterpolationContent(string source, BindingMetadata metadata)
    {
        ExpressionNode? content = null;
        var options = TransformOptions.CreateDom();
        options.PrefixIdentifiers = true;
        options.BindingMetadata = metadata;
        options.NodeTransforms = new NodeTransform[]
        {
            (node, _) =>
            {
                if (content is null && node is InterpolationNode interpolation)
                {
                    content = interpolation.Content;
                }

                return null;
            },
        };
        TransformTestHelpers.Transform(source, options);
        return content.ShouldNotBeNull();
    }

    private static string HandlerValue(string source, BindingMetadata metadata)
    {
        var result = TransformPrefixed(source, metadata, out _);
        return Flatten(PropertyValue(result, "onClick"));
    }

    private static string HandlerBody(string source, BindingMetadata metadata) => HandlerValue(source, metadata);

    private static object PropertyValue(TransformResult result, string key)
    {
        var vnode = result.RootCodegen().ShouldBeOfType<VNodeCall>();
        var properties = vnode.Props.ShouldBeOfType<ObjectExpression>();
        var property = properties.Properties.First(p => p.Key is SimpleExpressionNode s && s.Content == key);
        return property.Value;
    }

    private static string FlattenProps(TransformResult result)
    {
        var vnode = result.RootCodegen().ShouldBeOfType<VNodeCall>();
        return vnode.Props is null ? string.Empty : FlattenTree(vnode.Props);
    }

    // Concatenates a rewritten expression back to source text for assertion.
    private static string Flatten(object? part) => part switch
    {
        null => string.Empty,
        string text => text,
        SimpleExpressionNode expression => expression.Content,
        CompoundExpressionNode compound => string.Concat(compound.Parts.Select(Flatten)),
        RuntimeHelper helper => "_" + helper.Name,
        InterpolationNode interpolation => Flatten(interpolation.Content),
        TextNode text => text.Content,
        _ => throw new InvalidOperationException("Unexpected expression part: " + part.GetType().Name),
    };

    // Stringifies an arbitrary code-generation node loosely, for containment assertions over built props.
    private static string FlattenTree(object? node) => node switch
    {
        null => string.Empty,
        string text => text,
        SimpleExpressionNode expression => expression.Content,
        CompoundExpressionNode compound => string.Concat(compound.Parts.Select(FlattenTree)),
        RuntimeHelper helper => "_" + helper.Name,
        InterpolationNode interpolation => FlattenTree(interpolation.Content),
        TextNode text => text.Content,
        ObjectExpression obj => "{" + string.Join(",", obj.Properties.Select(p => FlattenTree(p.Key) + ":" + FlattenTree(p.Value))) + "}",
        ArrayExpression array => "[" + string.Join(",", array.Elements.Select(FlattenTree)) + "]",
        CallExpression call => FlattenTree(call.Callee) + "(" + string.Join(",", call.Arguments.Select(FlattenTree)) + ")",
        VNodeCall vnode => FlattenVNode(vnode),
        _ => node.ToString() ?? string.Empty,
    };

    private static string FlattenVNode(VNodeCall vnode)
    {
        var parts = new List<string> { FlattenTree(vnode.Tag) };
        if (vnode.Props is not null)
        {
            parts.Add(FlattenTree(vnode.Props));
        }

        if (vnode.Children is not null)
        {
            parts.Add(FlattenTree(vnode.Children));
        }

        return string.Join(",", parts);
    }
}
