using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.Versioning;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Browser.Tests;

/// <summary>
/// Proves that direct and command-buffered browser operations produce identical DOM for unified
/// component-tree scenarios.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class CommandBufferDifferentialTests
{
    [Theory]
    [InlineData("element-text")]
    [InlineData("attributes")]
    [InlineData("style-string-then-map")]
    [InlineData("fragment-children")]
    [InlineData("comment")]
    [InlineData("static-content")]
    [InlineData("keyed-reorder")]
    [InlineData("keyed-insert-remove")]
    [InlineData("unkeyed-fragment")]
    [InlineData("array-to-text")]
    [InlineData("event-listeners")]
    [InlineData("replace-mismatched-type")]
    [InlineData("svg-xlink")]
    [InlineData("mount-then-unmount")]
    public void BufferedMode_ProducesByteIdenticalDom_ToDirectMode(
        string scenarioName)
    {
        DifferentialOutcome outcome = Run(GetScenario(scenarioName));

        outcome.BufferedSerialized.ShouldBe(outcome.DirectSerialized);
    }

    [Fact]
    public void BufferedCommit_MakesOneInteropCallRegardlessOfOperationCount()
    {
        const int itemCount = 300;
        DifferentialOutcome outcome = Run(
            (renderer, container, commit) =>
            {
                renderer.Render(LargeTree(itemCount, revision: 0), container);
                commit();
                renderer.Render(LargeTree(itemCount, revision: 1), container);
                commit();
            });

        outcome.BufferedSerialized.ShouldBe(outcome.DirectSerialized);
        outcome.InteropCalls.ShouldBe(2);
        outcome.ApplyOperationCounts.Count.ShouldBe(2);
        outcome.ApplyOperationCounts[0].ShouldBeGreaterThan(itemCount);
        outcome.ApplyOperationCounts[1].ShouldBe(itemCount);
    }

    [Fact]
    public void RendererCommit_AppliesBufferedFrameWithoutManualBoundaryCall()
    {
        InMemoryHandleDom dom = new();
        int container = dom.CreateElement("root", null);
        BufferedBrowserNodeOperations buffered = new(
            (frame, length) =>
                CommandBufferDecoder.Apply(frame, length, dom),
            static _ => 0,
            dom.ParentNode,
            dom.NextSibling,
            dom.InsertStaticContent);
        buffered.Activate();
        buffered.ObserveForeignHandle(container);
        Renderer<int> renderer =
            RendererFactory.CreateRenderer(buffered.Create());
        try
        {
            renderer.Render(
                Element("main", Text("committed")),
                container);

            buffered.InteropCallCount.ShouldBe(1);
            dom.Serialize(container).ShouldContain("committed");
            buffered.Buffer.HasPendingOperations.ShouldBeFalse();
        }
        finally
        {
            buffered.Deactivate();
        }
    }

    private static Action<Renderer<int>, int, Action> GetScenario(string name)
    {
        return name switch
        {
            "element-text" => ElementText,
            "attributes" => Attributes,
            "style-string-then-map" => StyleStringThenMap,
            "fragment-children" => FragmentChildren,
            "comment" => CommentNode,
            "static-content" => StaticContent,
            "keyed-reorder" => KeyedReorder,
            "keyed-insert-remove" => KeyedInsertRemove,
            "unkeyed-fragment" => UnkeyedFragment,
            "array-to-text" => ArrayToText,
            "event-listeners" => EventListeners,
            "replace-mismatched-type" => ReplaceMismatchedType,
            "svg-xlink" => SvgXlink,
            "mount-then-unmount" => MountThenUnmount,
            _ => throw new ArgumentOutOfRangeException(
                nameof(name),
                name,
                "Unknown scenario."),
        };
    }

    private static void ElementText(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Render(renderer, container, commit, Element("div", Element("span", Text("hi"))));
        Render(renderer, container, commit, Element("div", Element("span", Text("bye"))));
    }

    private static void Attributes(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Render(
            renderer,
            container,
            commit,
            Element(
                "input",
                Attributes(
                    ("id", "a"),
                    ("class", "row"),
                    ("data-index", 1),
                    ("disabled", true),
                    ("title", "first"))));
        Render(
            renderer,
            container,
            commit,
            Element(
                "input",
                Attributes(
                    ("id", "b"),
                    ("class", "row active"),
                    ("disabled", false),
                    ("title", "second"))));
    }

    private static void StyleStringThenMap(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Render(
            renderer,
            container,
            commit,
            Element("div", Attributes(("style", "color:red")), Text("x")));
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                Attributes(
                    ("style", StyleMap(("color", "blue"), ("font-weight", "bold")))),
                Text("x")));
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                Attributes(("style", StyleMap(("color", "green !important")))),
                Text("x")));
    }

    private static void FragmentChildren(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                ComponentTree.Fragment(
                    [Element("span", Text("a")), Element("span", Text("b"))])));
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                ComponentTree.Fragment(
                    [
                        Element("span", Text("a")),
                        Element("span", Text("c")),
                        Element("span", Text("d")),
                    ])));
    }

    private static void CommentNode(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                ComponentTree.Comment("first"),
                Element("span", Text("x"))));
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                ComponentTree.Comment("first"),
                Element("span", Text("y"))));
    }

    private static void StaticContent(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                ComponentTree.Static("<b>bold</b><i>italic</i>"),
                Element("span", Text("y"))));
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                ComponentTree.Static("<b>bold</b><i>italic</i>"),
                Element("span", Text("z"))));
    }

    private static void KeyedReorder(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Render(renderer, container, commit, Element("ul", Li("a"), Li("b"), Li("c"), Li("d")));
        Render(renderer, container, commit, Element("ul", Li("d"), Li("b"), Li("a"), Li("c")));
    }

    private static void KeyedInsertRemove(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Render(renderer, container, commit, Element("ul", Li("a"), Li("b"), Li("c")));
        Render(renderer, container, commit, Element("ul", Li("a"), Li("x"), Li("c")));
        Render(renderer, container, commit, Element("ul", Li("a"), Li("c")));
    }

    private static void UnkeyedFragment(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        ComponentOptimization optimization =
            new(PatchFlags.UnkeyedFragment);
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                ComponentTree.Fragment(
                    [Element("span", Text("a")), Element("span", Text("b"))],
                    optimization: optimization)));
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                ComponentTree.Fragment(
                    [Element("span", Text("a"))],
                    optimization: optimization)));
    }

    private static void ArrayToText(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                Element("span", Text("x")),
                Element("span", Text("y"))));
        Render(renderer, container, commit, Element("div", Text("just text")));
    }

    private static void EventListeners(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Action first = static () => { };
        Action second = static () => { };
        Render(
            renderer,
            container,
            commit,
            Element("button", Attributes(("onClick", first)), Text("go")));
        Render(
            renderer,
            container,
            commit,
            Element("button", Attributes(("onClick", second)), Text("go")));
        Render(
            renderer,
            container,
            commit,
            Element("button", Attributes(("type", "button")), Text("go")));
    }

    private static void ReplaceMismatchedType(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                Element("span", Text("a")),
                Element("p", Text("b"))));
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                Element("aside", Text("a")),
                Element("p", Text("b"))));
    }

    private static void SvgXlink(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Render(
            renderer,
            container,
            commit,
            Element(
                "svg",
                Element("use", Attributes(("xlink:href", "#a")))));
        Render(
            renderer,
            container,
            commit,
            Element(
                "svg",
                Element("use", Attributes(("xlink:href", "#b")))));
    }

    private static void MountThenUnmount(
        Renderer<int> renderer,
        int container,
        Action commit)
    {
        Render(
            renderer,
            container,
            commit,
            Element(
                "div",
                Element("span", Text("a")),
                Element(
                    "button",
                    Attributes(("onClick", (Action)(static () => { }))),
                    Text("b"))));
        renderer.Render(null, container);
        commit();
    }

    private static IComponent LargeTree(int count, int revision)
    {
        IComponent[] children = new IComponent[count];
        for (int index = 0; index < count; index++)
        {
            children[index] =
                Element("span", Text($"item {index}:{revision}"));
        }

        return ComponentTree.Element("div", children: children);
    }

    private static IElementComponent Element(
        string tag,
        params IComponent[] children)
    {
        return ComponentTree.Element(tag, children: children);
    }

    private static IElementComponent Element(
        string tag,
        IComponentAttributeCollection attributes,
        params IComponent[] children)
    {
        return ComponentTree.Element(tag, attributes, children);
    }

    private static IElementComponent Li(string key)
    {
        return ComponentTree.Element(
            "li",
            children: [Text(key)],
            key: key);
    }

    private static ITextComponent Text(string text)
    {
        return ComponentTree.Text(text);
    }

    private static ComponentAttributes Attributes(
        params (string Name, object? Value)[] entries)
    {
        ComponentAttribute[] attributes =
            new ComponentAttribute[entries.Length];
        for (int index = 0; index < entries.Length; index++)
        {
            attributes[index] =
                new ComponentAttribute(entries[index].Name, entries[index].Value);
        }

        return new ComponentAttributes(attributes);
    }

    private static IReadOnlyDictionary<string, object?> StyleMap(
        params (string Name, string Value)[] entries)
    {
        Dictionary<string, object?> map =
            new(StringComparer.Ordinal);
        foreach ((string name, string value) in entries)
        {
            map[name] = value;
        }

        return map;
    }

    private static void Render(
        Renderer<int> renderer,
        int container,
        Action commit,
        IComponent component)
    {
        renderer.Render(component, container);
        commit();
    }

    private static DifferentialOutcome Run(
        Action<Renderer<int>, int, Action> scenario)
    {
        string direct = RunDirect(scenario);
        (
            string buffered,
            int interopCalls,
            IReadOnlyList<int> applyOperationCounts) = RunBuffered(scenario);
        return new DifferentialOutcome(
            direct,
            buffered,
            interopCalls,
            applyOperationCounts);
    }

    private static string RunDirect(
        Action<Renderer<int>, int, Action> scenario)
    {
        InMemoryHandleDom dom = new();
        int container = dom.CreateElement("root", null);
        using DirectHandleDomWorld world = new(dom);
        Renderer<int> renderer =
            RendererFactory.CreateRenderer(world.Options);
        scenario(renderer, container, static () => { });
        return dom.Serialize(container);
    }

    private static (
        string Serialized,
        int InteropCalls,
        IReadOnlyList<int> ApplyOperationCounts) RunBuffered(
            Action<Renderer<int>, int, Action> scenario)
    {
        List<int> applyOperationCounts = [];
        InMemoryHandleDom dom = new();
        int container = dom.CreateElement("root", null);
        BufferedBrowserNodeOperations buffered = new(
            (frame, length) =>
            {
                applyOperationCounts.Add(
                    BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(2)));
                return CommandBufferDecoder.Apply(frame, length, dom);
            },
            static _ => 0,
            dom.ParentNode,
            dom.NextSibling,
            dom.InsertStaticContent);
        buffered.Activate();
        buffered.ObserveForeignHandle(container);
        Renderer<int> renderer =
            RendererFactory.CreateRenderer(buffered.Create());
        try
        {
            scenario(renderer, container, buffered.ApplyPending);
            buffered.ApplyPending();
            return (
                dom.Serialize(container),
                buffered.InteropCallCount,
                applyOperationCounts);
        }
        finally
        {
            buffered.Deactivate();
        }
    }

    private sealed record DifferentialOutcome(
        string DirectSerialized,
        string BufferedSerialized,
        int InteropCalls,
        IReadOnlyList<int> ApplyOperationCounts);
}
