using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins Vue 3.5's keyed-children reconciliation and longest-increasing-subsequence move behavior:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/renderer.ts.
/// </summary>
public sealed class KeyedDiffRegressionTests
{
    [Fact]
    public void Render_ReversedKeyedList_PreservesIdentityWithMinimalMoves()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            KeyedList("1", "2", "3", "4", "5"),
            host.Root);
        Dictionary<string, FakeHostNode> original =
            RenderedElements(host)
                .ToDictionary(host.Text, StringComparer.Ordinal);
        host.Operations.Clear();

        renderer.Render(
            KeyedList("5", "4", "3", "2", "1"),
            host.Root);

        FakeHostNode[] reordered = RenderedElements(host);
        reordered.Select(host.Text).ShouldBe(["5", "4", "3", "2", "1"]);
        for (int index = 0; index < reordered.Length; index++)
        {
            string key = host.Text(reordered[index]);
            reordered[index].ShouldBeSameAs(original[key]);
        }

        host.Operations.Count(
                operation => operation.StartsWith(
                    "insert:",
                    StringComparison.Ordinal))
            .ShouldBe(4);
        host.Operations.ShouldNotContain(
            operation => operation.StartsWith("remove:", StringComparison.Ordinal));
        host.Operations.ShouldNotContain(
            operation => operation.StartsWith("create:", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_RandomKeyedPermutationsWithInsertsAndDeletes_ConvergeWithoutRemountingRetainedKeys()
    {
        Random random = new(20260717);
        for (int iteration = 0; iteration < 100; iteration++)
        {
            FakeHost host = new();
            Renderer<FakeHostNode> renderer =
                RendererFactory.CreateRenderer(host.Options);
            string[] initial = RandomDistinctKeys(random, 0, 15);
            renderer.Render(KeyedList(initial), host.Root);
            Dictionary<string, FakeHostNode> original =
                RenderedElements(host)
                    .ToDictionary(host.Text, StringComparer.Ordinal);

            string[] next = RandomNextKeys(random, initial);
            renderer.Render(KeyedList(next), host.Root);

            FakeHostNode[] rendered = RenderedElements(host);
            rendered.Select(host.Text).ShouldBe(
                next,
                $"iteration {iteration}: "
                + $"[{string.Join(",", initial)}] -> "
                + $"[{string.Join(",", next)}]");
            for (int index = 0; index < next.Length; index++)
            {
                if (original.TryGetValue(
                        next[index],
                        out FakeHostNode? retained))
                {
                    rendered[index].ShouldBeSameAs(
                        retained,
                        $"retained key {next[index]} remounted "
                        + $"during iteration {iteration}");
                }
            }
        }
    }

    [Fact]
    public void Render_DuplicateKeys_WarnsThroughApplicationContext()
    {
        IElementComponent initial = KeyedList("initial");
        List<string> warnings = [];
        IApplicationContext application =
            Application(initial, warnings);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(initial, host.Root, application);

        renderer.Render(
            KeyedList("duplicate", "duplicate"),
            host.Root);

        warnings.ShouldContain(
            warning => warning.Contains(
                "Duplicate keys",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Render_MixedKeyedAndKeylessChildren_WarnsWhileCommentsRemainExempt()
    {
        IElementComponent initial = KeyedList("a", "b");
        List<string> warnings = [];
        IApplicationContext application =
            Application(initial, warnings);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(initial, host.Root, application);

        renderer.Render(
            ComponentTree.Element(
                "ul",
                children:
                [
                    ComponentTree.Comment(),
                    KeyedElement("a"),
                ]),
            host.Root);

        warnings.ShouldBeEmpty();

        renderer.Render(
            ComponentTree.Element(
                "ul",
                children:
                [
                    ComponentTree.Element(
                        "li",
                        children: [ComponentTree.Text("keyless")]),
                    KeyedElement("a"),
                ]),
            host.Root);

        warnings.ShouldContain(
            warning => warning.Contains(
                "Mixed keyed and unkeyed",
                StringComparison.Ordinal));
    }

    private static IElementComponent KeyedList(params string[] keys)
    {
        IComponent[] children = new IComponent[keys.Length];
        for (int index = 0; index < keys.Length; index++)
        {
            children[index] = ComponentTree.Element(
                "li",
                children: [ComponentTree.Text(keys[index])],
                key: keys[index]);
        }

        return ComponentTree.Element("ul", children: children);
    }

    private static IElementComponent KeyedElement(string key)
    {
        return ComponentTree.Element(
            "li",
            children: [ComponentTree.Text(key)],
            key: key);
    }

    private static FakeHostNode[] RenderedElements(FakeHost host)
    {
        return host.Root.Children
            .Single()
            .Children
            .Where(node => node.Kind == FakeHostNodeKind.Element)
            .ToArray();
    }

    private static string[] RandomDistinctKeys(
        Random random,
        int rangeStart,
        int rangeEnd)
    {
        int count = random.Next(1, 9);
        return Enumerable.Range(rangeStart, rangeEnd - rangeStart)
            .OrderBy(_ => random.Next())
            .Take(count)
            .Select(key => key.ToString())
            .ToArray();
    }

    private static string[] RandomNextKeys(
        Random random,
        string[] initial)
    {
        IEnumerable<string> retained =
            initial.Where(_ => random.Next(3) != 0);
        IEnumerable<string> additions = Enumerable.Range(100, 15)
            .Where(_ => random.Next(3) == 0)
            .Select(key => key.ToString());
        return retained
            .Concat(additions)
            .OrderBy(_ => random.Next())
            .ToArray();
    }

    private static IApplicationContext Application(
        IComponent root,
        List<string> warnings)
    {
        return new ApplicationContext(
            root,
            new ComponentFactory(Array.Empty<ComponentRegistration>()),
            new EmptyServiceProvider())
        {
            WarnHandler = warnings.Add,
        };
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
