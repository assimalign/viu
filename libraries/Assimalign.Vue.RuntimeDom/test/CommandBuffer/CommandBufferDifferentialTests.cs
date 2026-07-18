using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.Versioning;

using Shouldly;
using Xunit;

using Assimalign.Vue.Reactivity;
using Assimalign.Vue.RuntimeCore;
using Assimalign.Vue.Shared;
using Assimalign.Vue.Testing;

using static Assimalign.Vue.RuntimeCore.VirtualNodeFactory;

namespace Assimalign.Vue.RuntimeDom.Tests;

// The heart of [V01.01.04.05]: a rich battery of renderer scenarios runs through the DIRECT node-ops
// and through the BUFFERED node-ops (encode -> single-apply decode/replay) and must produce
// byte-identical serialized DOM — batching is behaviorally invisible. Plus the interop-call-counter
// criterion: hundreds of mutations in one flush collapse into exactly one apply call. Both worlds
// share the ambient scheduler, so they run sequentially with a reset between (single-threaded model).
// Browser-annotated like the other tests that touch the browser-only node-ops types; nothing here
// crosses a real interop boundary (the buffered applier is the in-memory CommandBufferDecoder).
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
    [InlineData("component-reactive")]
    [InlineData("event-listeners")]
    [InlineData("v-show-toggle")]
    [InlineData("replace-mismatched-type")]
    [InlineData("svg-xlink")]
    [InlineData("mount-then-unmount")]
    public void BufferedMode_ProducesByteIdenticalDom_ToDirectMode(string scenarioName)
    {
        var outcome = Run(GetScenario(scenarioName));

        outcome.BufferedSerialized.ShouldBe(outcome.DirectSerialized);
    }

    // The interop-call-counter acceptance criterion: one boundary crossing per scheduler flush
    // regardless of op count, on a workload of hundreds of mutations per flush.
    [Fact]
    public void BufferedFlush_MakesExactlyOneInteropCall_PerFlush_RegardlessOfOpCount()
    {
        const int itemCount = 300;
        var outcome = Run((renderer, container, pump) =>
        {
            var revision = Reactive.Reference(0);
            var component = new RenderComponent((_, _) => () =>
            {
                var items = new VirtualNode?[itemCount];
                for (var index = 0; index < items.Length; index++)
                {
                    items[index] = Element("span", $"item {index}:{revision.Value}");
                }
                return Element("div", (VirtualNodeProperties?)null, items);
            });
            renderer.Render(Component(component), container); // flush 1: mount
            revision.Value = 1;
            pump.RunUntilIdle();                              // flush 2: reactive update
        });

        outcome.BufferedSerialized.ShouldBe(outcome.DirectSerialized);
        // Two flushes, each exactly one apply — never one-per-mutation.
        outcome.InteropCalls.ShouldBe(2);
        outcome.ApplyOperationCounts.Count.ShouldBe(2);
        outcome.ApplyOperationCounts[0].ShouldBeGreaterThan(itemCount); // mount: creates + text + inserts
        outcome.ApplyOperationCounts[1].ShouldBe(itemCount);            // update: itemCount text writes, one call
    }

    // --- scenarios -------------------------------------------------------------------------------

    private static Action<Renderer<int>, int, TestSchedulerPump> GetScenario(string name) => name switch
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
        "component-reactive" => ComponentReactive,
        "event-listeners" => EventListeners,
        "v-show-toggle" => VShowToggle,
        "replace-mismatched-type" => ReplaceMismatchedType,
        "svg-xlink" => SvgXlink,
        "mount-then-unmount" => MountThenUnmount,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown scenario."),
    };

    private static void ElementText(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(Element("div", (VirtualNodeProperties?)null, Element("span", "hi")), container);
        renderer.Render(Element("div", (VirtualNodeProperties?)null, Element("span", "bye")), container);
    }

    private static void Attributes(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(
            Element("input", Properties(("id", "a"), ("class", "row"), ("data-index", 1), ("disabled", true), ("title", "first"))),
            container);
        renderer.Render(
            Element("input", Properties(("id", "b"), ("class", "row active"), ("disabled", false), ("title", "second"))),
            container);
    }

    private static void StyleStringThenMap(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(Element("div", Properties(("style", "color:red")), "x"), container);
        renderer.Render(Element("div", Properties(("style", StyleMap(("color", "blue"), ("font-weight", "bold")))), "x"), container);
        renderer.Render(Element("div", Properties(("style", StyleMap(("color", "green !important")))), "x"), container);
    }

    private static void FragmentChildren(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(Element("div", (VirtualNodeProperties?)null, Fragment(Element("span", "a"), Element("span", "b"))), container);
        renderer.Render(Element("div", (VirtualNodeProperties?)null, Fragment(Element("span", "a"), Element("span", "c"), Element("span", "d"))), container);
    }

    private static void CommentNode(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(Element("div", (VirtualNodeProperties?)null, Comment("first"), Element("span", "x")), container);
        renderer.Render(Element("div", (VirtualNodeProperties?)null, Comment("first"), Element("span", "y")), container);
    }

    private static void StaticContent(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(Element("div", (VirtualNodeProperties?)null, Static("<b>bold</b><i>italic</i>"), Element("span", "y")), container);
        renderer.Render(Element("div", (VirtualNodeProperties?)null, Static("<b>bold</b><i>italic</i>"), Element("span", "z")), container);
    }

    private static void KeyedReorder(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(Element("ul", (VirtualNodeProperties?)null, Li("a"), Li("b"), Li("c"), Li("d")), container);
        renderer.Render(Element("ul", (VirtualNodeProperties?)null, Li("d"), Li("b"), Li("a"), Li("c")), container);
    }

    private static void KeyedInsertRemove(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(Element("ul", (VirtualNodeProperties?)null, Li("a"), Li("b"), Li("c")), container);
        renderer.Render(Element("ul", (VirtualNodeProperties?)null, Li("a"), Li("x"), Li("c")), container);
        renderer.Render(Element("ul", (VirtualNodeProperties?)null, Li("a"), Li("c")), container);
    }

    private static void UnkeyedFragment(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(
            Element("div", (VirtualNodeProperties?)null, Fragment(new VirtualNode?[] { Element("span", "a"), Element("span", "b") }, null, PatchFlags.UnkeyedFragment)),
            container);
        renderer.Render(
            Element("div", (VirtualNodeProperties?)null, Fragment(new VirtualNode?[] { Element("span", "a") }, null, PatchFlags.UnkeyedFragment)),
            container);
    }

    private static void ArrayToText(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(Element("div", (VirtualNodeProperties?)null, Element("span", "x"), Element("span", "y")), container);
        renderer.Render(Element("div", (VirtualNodeProperties?)null, "just text"), container);
    }

    private static void ComponentReactive(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        var count = Reactive.Reference(0);
        var component = new RenderComponent((_, _) => () => Element("div", (VirtualNodeProperties?)null, Element("span", $"count {count.Value}")));
        renderer.Render(Component(component), container);
        count.Value = 5;
        pump.RunUntilIdle();
    }

    private static void EventListeners(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        Action first = static () => { };
        Action second = static () => { };
        renderer.Render(Element("button", Properties(("onClick", first)), "go"), container);
        renderer.Render(Element("button", Properties(("onClick", second)), "go"), container); // delegate swap
        renderer.Render(Element("button", Properties(("type", "button")), "go"), container);   // listener removed
    }

    private static void VShowToggle(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        var condition = Reactive.Reference<object?>(true);
        var component = new RenderComponent((_, _) => () =>
            Directives.WithDirectives(Element("div", (VirtualNodeProperties?)null, "content"), VShow.Instance, condition.Value));
        renderer.Render(Component(component), container);
        pump.RunUntilIdle();
        condition.Value = false;
        pump.RunUntilIdle();
        condition.Value = true;
        pump.RunUntilIdle();
    }

    private static void ReplaceMismatchedType(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(Element("div", (VirtualNodeProperties?)null, Element("span", "a"), Element("p", "b")), container);
        // span -> aside is a type mismatch: the renderer reads NextSibling(span) for the mount anchor,
        // which forces the buffered path to flush before reading. The DOM must still match direct mode.
        renderer.Render(Element("div", (VirtualNodeProperties?)null, Element("aside", "a"), Element("p", "b")), container);
    }

    private static void SvgXlink(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(Element("svg", (VirtualNodeProperties?)null, Element("use", Properties(("xlink:href", "#a")))), container);
        renderer.Render(Element("svg", (VirtualNodeProperties?)null, Element("use", Properties(("xlink:href", "#b")))), container);
    }

    private static void MountThenUnmount(Renderer<int> renderer, int container, TestSchedulerPump pump)
    {
        renderer.Render(
            Element("div", (VirtualNodeProperties?)null, Element("span", "a"), Element("button", Properties(("onClick", (Action)(static () => { }))), "b")),
            container);
        renderer.Render(null, container); // full unmount: teardown + released-handle purge
    }

    private static VirtualNode Li(string key) => Element("li", Properties(("key", key)), key);

    private static IReadOnlyDictionary<string, object?> StyleMap(params (string Name, string Value)[] entries)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in entries)
        {
            map[name] = value;
        }
        return map;
    }

    // --- differential runner ---------------------------------------------------------------------

    private static DifferentialOutcome Run(Action<Renderer<int>, int, TestSchedulerPump> scenario)
    {
        var direct = RunDirect(scenario);
        var (buffered, interopCalls, applyOperationCounts) = RunBuffered(scenario);
        return new DifferentialOutcome(direct, buffered, interopCalls, applyOperationCounts);
    }

    private static string RunDirect(Action<Renderer<int>, int, TestSchedulerPump> scenario)
    {
        Scheduler.Reset();
        var pump = TestSchedulerPump.Install();
        var dom = new InMemoryHandleDom();
        var container = dom.CreateElement("root", null);
        using var world = new DirectHandleDomWorld(dom);
        var renderer = RendererFactory.CreateRenderer(world.Options);
        try
        {
            scenario(renderer, container, pump);
            pump.RunUntilIdle();
            return dom.Serialize(container);
        }
        finally
        {
            pump.Dispose();
            Scheduler.Reset();
        }
    }

    private static (string Serialized, int InteropCalls, IReadOnlyList<int> ApplyOperationCounts) RunBuffered(
        Action<Renderer<int>, int, TestSchedulerPump> scenario)
    {
        Scheduler.Reset();
        var pump = TestSchedulerPump.Install();
        var applyOperationCounts = new List<int>();
        var dom = new InMemoryHandleDom();
        var container = dom.CreateElement("root", null);
        var buffered = new BufferedBrowserNodeOperations(
            (frame, length) =>
            {
                applyOperationCounts.Add(BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(2)));
                return CommandBufferDecoder.Apply(frame, length, dom);
            },
            static _ => 0,
            dom.ParentNode,
            dom.NextSibling,
            dom.InsertStaticContent);
        buffered.Activate();
        buffered.ObserveForeignHandle(container);
        var renderer = RendererFactory.CreateRenderer(buffered.Create());
        try
        {
            scenario(renderer, container, pump);
            pump.RunUntilIdle();
            return (dom.Serialize(container), buffered.InteropCallCount, applyOperationCounts);
        }
        finally
        {
            buffered.Deactivate();
            pump.Dispose();
            Scheduler.Reset();
        }
    }

    private sealed record DifferentialOutcome(
        string DirectSerialized,
        string BufferedSerialized,
        int InteropCalls,
        IReadOnlyList<int> ApplyOperationCounts);
}
