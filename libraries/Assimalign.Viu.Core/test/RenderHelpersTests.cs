using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Tests;

public sealed class RenderHelpersTests
{
    public RenderHelpersTests()
    {
        RenderHelpers.ResetBlockTrackingForTests();
    }

    [Fact]
    public void NormalizeRoot_GeneratedShapes_ProduceUnifiedComponentValues()
    {
        RenderHelpers.NormalizeRoot(null).ShouldBeAssignableTo<ICommentComponent>();
        RenderHelpers.NormalizeRoot(false).ShouldBeAssignableTo<ICommentComponent>();
        RenderHelpers.NormalizeRoot("hello")
            .ShouldBeAssignableTo<ITextComponent>()
            .Text.ShouldBe("hello");

        IFragmentComponent fragment = RenderHelpers.NormalizeRoot(
            new object?[] { "one", null, 2 }).ShouldBeAssignableTo<IFragmentComponent>();

        fragment.Children.Count.ShouldBe(3);
        fragment.Children[0].ShouldBeAssignableTo<ITextComponent>().Text.ShouldBe("one");
        fragment.Children[1].ShouldBeAssignableTo<ICommentComponent>();
        fragment.Children[2].ShouldBeAssignableTo<ITextComponent>().Text.ShouldBe("2");
    }

    [Fact]
    public void CreateElementVNode_SeparatesKeyFromAttributes_AndNormalizesChildren()
    {
        Action<object?> click = _ => { };

        IElementComponent element = RenderHelpers._createElementVNode(
            "button",
            RenderHelpers._createProps(
                ("key", 7),
                ("id", "save"),
                ("onClick", click)),
            "Save",
            (int)PatchFlags.Props,
            ["id"]).ShouldBeAssignableTo<IElementComponent>();

        element.Key.ShouldBe(7);
        element.Attributes.Count.ShouldBe(2);
        element.Attributes.TryGetValue("key", out _).ShouldBeFalse();
        element.Attributes.TryGetValue("id", out object? id).ShouldBeTrue();
        id.ShouldBe("save");
        element.Attributes.TryGetValue("onClick", out object? listener).ShouldBeTrue();
        listener.ShouldBeSameAs(click);
        element.Children.Count.ShouldBe(1);
        element.Children[0].ShouldBeAssignableTo<ITextComponent>().Text.ShouldBe("Save");
        element.Optimization.PatchFlags.ShouldBe(PatchFlags.Props);
        element.Optimization.DynamicProperties.ShouldBe(["id"]);
        element.Optimization.DynamicChildren.ShouldBeNull();
    }

    [Fact]
    public void CreateBlock_ResolvedName_CarriesRawPropertiesAndTypedListenersWithoutActivation()
    {
        TaskCompletionSource completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Func<object?, Task> save = RenderHelpers._withHandler(
            (object? _) => completion.Task);
        ComponentSlot defaultSlot = RenderHelpers._withCtx(
            () => new object?[] { RenderHelpers._createTextVNode("content") });

        ITemplateComponent request = RenderHelpers._createBlock(
            RenderHelpers._openBlock(),
            RenderHelpers._resolveComponent("MyButton"),
            RenderHelpers._createProps(
                ("key", "primary"),
                ("kind", "success"),
                ("onSave", save)),
            RenderHelpers._createProps(
                ("default", defaultSlot),
                ("_", 1)),
            (int)PatchFlags.Props,
            ["kind"]).ShouldBeAssignableTo<ITemplateComponent>();

        request.TemplateName.ShouldBe("MyButton");
        request.TemplateType.ShouldBeNull();
        request.Key.ShouldBe("primary");
        request.Arguments.Count.ShouldBe(2);
        request.Arguments["kind"].ShouldBe("success");
        request.Arguments["onSave"].ShouldBeSameAs(save);
        request.Listeners.ShouldNotBeNull();
        ComponentEventListener saveListener = request.Listeners!["save"];
        saveListener.IsAsynchronous.ShouldBeTrue();
        saveListener.AsynchronousHandler!(42).ShouldBeSameAs(completion.Task);
        request.Slots.ShouldNotBeNull();
        IComponent? slotContent = request.Slots!["default"](new ComponentArguments());
        slotContent.ShouldNotBeNull();
        slotContent.ShouldBeAssignableTo<ITextComponent>().Text.ShouldBe("content");
    }

    [Fact]
    public void CreateVNode_TemplateType_ProducesDeferredTypedRequest()
    {
        ITemplateComponent request = RenderHelpers._createVNode(
            typeof(TestTemplate),
            RenderHelpers._createProps(("value", 42)))
            .ShouldBeAssignableTo<ITemplateComponent>();

        request.TemplateType.ShouldBe(typeof(TestTemplate));
        request.TemplateName.ShouldBeNull();
        request.Arguments.Get<int>("value").ShouldBe(42);
    }

    [Fact]
    public void PrimitiveFactories_CreateTextCommentStaticAndFragmentValues()
    {
        ITextComponent text = RenderHelpers._createTextVNode(12, (int)PatchFlags.Text);
        ICommentComponent comment = RenderHelpers._createCommentVNode("note");
        ICommentComponent blockComment = RenderHelpers._createCommentVNode("v-if", true);
        IStaticComponent content = RenderHelpers._createStaticVNode("<b>x</b>", 1);
        IFragmentComponent fragment = RenderHelpers._createElementVNode(
            RenderHelpers._Fragment,
            null,
            new object?[] { text, comment, content })
            .ShouldBeAssignableTo<IFragmentComponent>();

        text.Text.ShouldBe("12");
        text.Optimization.PatchFlags.ShouldBe(PatchFlags.Text);
        comment.Text.ShouldBe("note");
        blockComment.Optimization.IsBlock.ShouldBeTrue();
        blockComment.Optimization.DynamicChildren.ShouldNotBeNull();
        blockComment.Optimization.DynamicChildren.ShouldBeEmpty();
        content.Content.ShouldBe("<b>x</b>");
        fragment.Children.ShouldBe([text, comment, content]);
    }

    [Fact]
    public void RenderList_IterableIndexRangeAndDictionaryOverloads_PreserveOrdering()
    {
        string[] values = RenderHelpers._renderList(
            new[] { "a", "b" },
            (value, index) => $"{index}:{value}");
        int[] range = RenderHelpers._renderList(3, value => value);
        string[] entries = RenderHelpers._renderList(
            new Dictionary<string, int>
            {
                ["a"] = 1,
                ["b"] = 2,
            },
            (value, key, index) => $"{index}:{key}={value}");

        values.ShouldBe(["0:a", "1:b"]);
        range.ShouldBe([1, 2, 3]);
        entries.ShouldBe(["0:a=1", "1:b=2"]);
    }

    [Fact]
    public void RenderSlot_PassesGeneratedArguments_AndUsesFallbackWhenAbsent()
    {
        Dictionary<string, ComponentSlot> slots = new(StringComparer.Ordinal)
        {
            ["header"] = arguments =>
                ComponentTree.Text(arguments.Get<string>("title") ?? string.Empty),
        };

        IComponent rendered = RenderHelpers._renderSlot(
            slots,
            "header",
            RenderHelpers._createProps(("title", "Hello")));
        IComponent fallback = RenderHelpers._renderSlot(
            slots,
            "missing",
            RenderHelpers._createProps(),
            () => new object?[] { "Fallback" });

        rendered.ShouldBeAssignableTo<ITextComponent>().Text.ShouldBe("Hello");
        fallback.ShouldBeAssignableTo<ITextComponent>().Text.ShouldBe("Fallback");
    }

    [Fact]
    public void RenderSlot_CommentOnlyContent_UsesFallback()
    {
        ComponentSlots slots = new()
        {
            ["default"] = _ => ComponentTree.Fragment(
            [
                ComponentTree.Comment(),
                ComponentTree.Fragment(
                [
                    ComponentTree.Comment("conditional"),
                ]),
            ]),
        };

        IComponent rendered = RenderHelpers._renderSlot(
            slots,
            "default",
            fallback: () => new object?[] { "Fallback" });

        rendered.ShouldBeAssignableTo<ITextComponent>().Text.ShouldBe("Fallback");
    }

    [Fact]
    public void CreateSlots_MergesGeneratedDynamicSlotDescriptors()
    {
        ComponentSlot defaultSlot = RenderHelpers._withCtx(
            () => new object?[] { "default" });
        ComponentSlot headerSlot = RenderHelpers._withCtx(
            () => new object?[] { "header" });
        IReadOnlyDictionary<string, object?> slots = RenderHelpers._createSlots(
            RenderHelpers._createProps(
                ("default", defaultSlot),
                ("_", 2)),
            new object?[]
            {
                RenderHelpers._createProps(
                    ("name", "header"),
                    ("fn", headerSlot)),
            });

        ITemplateComponent request = RenderHelpers._createVNode(
            RenderHelpers._resolveComponent("Panel"),
            null,
            slots).ShouldBeAssignableTo<ITemplateComponent>();

        request.Slots.ShouldNotBeNull();
        request.Slots!.Keys.ShouldBe(["default", "header"], ignoreOrder: true);
        request.Slots.ShouldBeAssignableTo<IComponentSlotCollection>()
            .Flags.ShouldBe(SlotFlags.Dynamic);
        IComponent? header = request.Slots["header"](new ComponentArguments());
        header.ShouldNotBeNull();
        header.ShouldBeAssignableTo<ITextComponent>().Text.ShouldBe("header");
    }

    [Fact]
    public void BlockFactory_DistinguishesPlainNodeFromEmptyAndTrackingDisabledBlocks()
    {
        IElementComponent plain = RenderHelpers._createElementVNode("span")
            .ShouldBeAssignableTo<IElementComponent>();
        IElementComponent emptyBlock = RenderHelpers._createElementBlock(
            RenderHelpers._openBlock(),
            "div").ShouldBeAssignableTo<IElementComponent>();
        IFragmentComponent disabledCollectionBlock = RenderHelpers._createElementBlock(
            RenderHelpers._openBlock(true),
            RenderHelpers._Fragment,
            null,
            new object?[]
            {
                RenderHelpers._createTextVNode("dynamic", (int)PatchFlags.Text),
            }).ShouldBeAssignableTo<IFragmentComponent>();

        plain.Optimization.DynamicChildren.ShouldBeNull();
        plain.Optimization.IsBlock.ShouldBeFalse();
        emptyBlock.Optimization.DynamicChildren.ShouldNotBeNull();
        emptyBlock.Optimization.DynamicChildren.ShouldBeEmpty();
        emptyBlock.Optimization.IsBlock.ShouldBeTrue();
        disabledCollectionBlock.Optimization.DynamicChildren.ShouldNotBeNull();
        disabledCollectionBlock.Optimization.DynamicChildren.ShouldBeEmpty();
        disabledCollectionBlock.Optimization.IsBlock.ShouldBeTrue();
    }

    [Fact]
    public void NestedBlocks_CollectTheInnerRootButNotItsDynamicDescendants()
    {
        BlockToken outerToken = RenderHelpers._openBlock();
        ITextComponent innerText = RenderHelpers._createTextVNode(
            "dynamic",
            (int)PatchFlags.Text);
        IElementComponent inner = RenderHelpers._createElementBlock(
            RenderHelpers._openBlock(),
            "section",
            null,
            new object?[]
            {
                RenderHelpers._createTextVNode("inner", (int)PatchFlags.Text),
            }).ShouldBeAssignableTo<IElementComponent>();
        IElementComponent outer = RenderHelpers._createElementBlock(
            outerToken,
            "main",
            null,
            new object?[] { innerText, inner })
            .ShouldBeAssignableTo<IElementComponent>();

        inner.Optimization.DynamicChildren.ShouldNotBeNull();
        inner.Optimization.DynamicChildren!.Count.ShouldBe(1);
        inner.Optimization.DynamicChildren[0]
            .ShouldBeAssignableTo<ITextComponent>()
            .Text.ShouldBe("inner");
        outer.Optimization.DynamicChildren.ShouldNotBeNull();
        outer.Optimization.DynamicChildren.ShouldBe([innerText, inner]);
    }

    [Fact]
    public void VOnce_SuspendsCollectionMarksBlockAndResumesTracking()
    {
        BlockToken outerToken = RenderHelpers._openBlock();
        IComponent cached = (IComponent)RenderHelpers._setCache(
            0,
            RenderHelpers._setBlockTracking(-1, true),
            RenderHelpers._createTextVNode("frozen", (int)PatchFlags.Text))!;
        IElementComponent root = RenderHelpers._createElementBlock(
            outerToken,
            "div",
            null,
            new object?[] { cached })
            .ShouldBeAssignableTo<IElementComponent>();

        root.Optimization.HasOnce.ShouldBeTrue();
        root.Optimization.DynamicChildren.ShouldNotBeNull();
        root.Optimization.DynamicChildren.ShouldBeEmpty();

        IElementComponent next = RenderHelpers._createElementBlock(
            RenderHelpers._openBlock(),
            "div",
            null,
            new object?[]
            {
                RenderHelpers._createTextVNode("live", (int)PatchFlags.Text),
            }).ShouldBeAssignableTo<IElementComponent>();
        next.Optimization.DynamicChildren!.Count.ShouldBe(1);
    }

    [Fact]
    public void WithDirectives_CarriesMetadata_AndReplacesTrackedImmutableValue()
    {
        BlockToken outerToken = RenderHelpers._openBlock();
        IElementComponent input = RenderHelpers._withDirectives(
            RenderHelpers._createElementVNode(
                "input",
                null,
                null,
                (int)PatchFlags.NeedPatch),
            new object?[]
            {
                new object?[]
                {
                    RenderHelpers._resolveDirective("focus"),
                    true,
                    "value",
                    new Dictionary<string, bool> { ["lazy"] = true },
                },
            }).ShouldBeAssignableTo<IElementComponent>();
        IElementComponent outer = RenderHelpers._createElementBlock(
            outerToken,
            "div",
            null,
            new object?[] { input })
            .ShouldBeAssignableTo<IElementComponent>();

        input.Directives.Count.ShouldBe(1);
        input.Directives[0].DirectiveName.ShouldBe("focus");
        input.Directives[0].Value.ShouldBe(true);
        input.Directives[0].Argument.ShouldBe("value");
        input.Directives[0].Modifiers["lazy"].ShouldBeTrue();
        outer.Optimization.DynamicChildren.ShouldBe([input]);
    }

    [Fact]
    public void WithHandler_TaskOverloads_PreserveTaskDelegateTypes()
    {
        int invocationCount = 0;
        Func<object?, object?> value =
            RenderHelpers._withHandler(_ => invocationCount++);
        Action<object?> synchronous =
            RenderHelpers._withHandler(_ => { invocationCount++; });
        Func<Task> noPayload = RenderHelpers._withHandler(HandleAsync);
        Func<object?, Task> payload = RenderHelpers._withHandler(HandlePayloadAsync);
        Func<object?, Task> typedPayload =
            RenderHelpers._withHandler<int>(HandleTypedPayloadAsync);

        value.ShouldBeOfType<Func<object?, object?>>();
        synchronous.ShouldBeOfType<Action<object?>>();
        noPayload.ShouldBeOfType<Func<Task>>();
        payload.ShouldBeOfType<Func<object?, Task>>();
        typedPayload.ShouldBeOfType<Func<object?, Task>>();
    }

    [Fact]
    public void WithMemo_UnchangedDependenciesReuseTheCachedTree()
    {
        object?[] cache = new object?[1];
        int renderCount = 0;

        IComponent first = RenderHelpers._withMemo(
            [1, "a"],
            () =>
            {
                renderCount++;
                return RenderHelpers._createElementVNode("div");
            },
            cache,
            0);
        IComponent second = RenderHelpers._withMemo(
            [1, "a"],
            () =>
            {
                renderCount++;
                return RenderHelpers._createElementVNode("div");
            },
            cache,
            0);
        IComponent third = RenderHelpers._withMemo(
            [2, "a"],
            () =>
            {
                renderCount++;
                return RenderHelpers._createElementVNode("div");
            },
            cache,
            0);

        second.ShouldBeSameAs(first);
        third.ShouldNotBeSameAs(first);
        renderCount.ShouldBe(2);
    }

    private static Task HandleAsync()
    {
        return Task.CompletedTask;
    }

    private static Task HandlePayloadAsync(object? value)
    {
        _ = value;
        return Task.CompletedTask;
    }

    private static Task HandleTypedPayloadAsync(int value)
    {
        _ = value;
        return Task.CompletedTask;
    }

    private sealed class TestTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            _ = context;
            return static () => null;
        }
    }
}
