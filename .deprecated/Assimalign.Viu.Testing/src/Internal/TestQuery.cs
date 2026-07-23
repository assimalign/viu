using System;
using System.Collections.Generic;
using System.Text;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Shared query helpers over the rendered test tree — selector matching plus the walks that back
/// the wrappers' <c>Find</c>/<c>FindComponent</c>/<c>Html</c>/<c>Text</c>. Selector support mirrors
/// the CSS subset <c>@vue/test-utils</c> find accepts (https://test-utils.vuejs.org/api/#find):
/// tag, <c>#id</c>, <c>.class</c>, and <c>[attr]</c>/<c>[attr=value]</c>.
/// </summary>
internal static class TestQuery
{
    /// <summary>Whether <paramref name="element"/> matches the single <paramref name="selector"/>.</summary>
    public static bool Matches(TestElement element, string selector)
    {
        if (string.IsNullOrEmpty(selector))
        {
            return false;
        }
        return selector[0] switch
        {
            '#' => string.Equals(AttributeString(element, "id"), selector[1..], StringComparison.Ordinal),
            '.' => HasClass(element, selector[1..]),
            '[' => MatchesAttribute(element, selector),
            _ => string.Equals(element.Tag, selector, StringComparison.Ordinal),
        };
    }

    /// <summary>Every element the component rendered (its own root and all descendants, into child components).</summary>
    public static List<TestElement> DescendantElements(VirtualNode? subtree)
    {
        var result = new List<TestElement>();
        CollectElements(subtree, result);
        return result;
    }

    /// <summary>Every element strictly inside <paramref name="root"/> (querySelector semantics — excludes self).</summary>
    public static List<TestElement> DescendantElementsOf(TestElement root)
    {
        var result = new List<TestElement>();
        foreach (var child in root.Children)
        {
            CollectElementNodes(child, result);
        }
        return result;
    }

    /// <summary>The top-level host nodes of the component's output (unwrapping nested components and fragments).</summary>
    public static List<TestNode> HostNodes(VirtualNode? subtree)
    {
        var result = new List<TestNode>();
        CollectHostNodes(subtree, result);
        return result;
    }

    /// <summary>The mounted descendant component instances, in render order (excludes the starting instance).</summary>
    public static List<ComponentInstance> DescendantComponents(VirtualNode? subtree)
    {
        var result = new List<ComponentInstance>();
        CollectComponents(subtree, result);
        return result;
    }

    /// <summary>Appends the text content of a node subtree (comments contribute nothing).</summary>
    public static void AppendText(TestNode node, StringBuilder builder)
    {
        switch (node)
        {
            case TestText text:
                builder.Append(text.Text);
                break;
            case TestElement element:
                foreach (var child in element.Children)
                {
                    AppendText(child, builder);
                }
                break;
        }
    }

    private static void CollectElements(VirtualNode? node, List<TestElement> into)
    {
        if (node is null)
        {
            return;
        }
        switch (node.Type)
        {
            case VirtualNodeType.Element:
                if (node.El is TestElement element)
                {
                    into.Add(element);
                }
                CollectChildren(node.ArrayChildren, into);
                break;
            case VirtualNodeType.Component:
                CollectElements((node.Component as ComponentInstance)?.Subtree, into);
                break;
            case VirtualNodeType.Fragment:
                CollectChildren(node.ArrayChildren, into);
                break;
        }
    }

    private static void CollectChildren(VirtualNode[]? children, List<TestElement> into)
    {
        if (children is null)
        {
            return;
        }
        foreach (var child in children)
        {
            CollectElements(child, into);
        }
    }

    private static void CollectElementNodes(TestNode node, List<TestElement> into)
    {
        if (node is not TestElement element)
        {
            return;
        }
        into.Add(element);
        foreach (var child in element.Children)
        {
            CollectElementNodes(child, into);
        }
    }

    private static void CollectHostNodes(VirtualNode? node, List<TestNode> into)
    {
        if (node is null)
        {
            return;
        }
        switch (node.Type)
        {
            case VirtualNodeType.Component:
                CollectHostNodes((node.Component as ComponentInstance)?.Subtree, into);
                break;
            case VirtualNodeType.Fragment:
                if (node.ArrayChildren is not null)
                {
                    foreach (var child in node.ArrayChildren)
                    {
                        CollectHostNodes(child, into);
                    }
                }
                break;
            default:
                if (node.El is TestNode host)
                {
                    into.Add(host);
                }
                break;
        }
    }

    private static void CollectComponents(VirtualNode? node, List<ComponentInstance> into)
    {
        if (node is null)
        {
            return;
        }
        if (node.Type == VirtualNodeType.Component && node.Component is ComponentInstance instance)
        {
            into.Add(instance);
            CollectComponents(instance.Subtree, into);
            return;
        }
        if (node.ArrayChildren is not null)
        {
            foreach (var child in node.ArrayChildren)
            {
                CollectComponents(child, into);
            }
        }
    }

    private static string? AttributeString(TestElement element, string name)
        => element.Properties.TryGetValue(name, out var value) ? value?.ToString() : null;

    private static bool HasClass(TestElement element, string className)
    {
        if (element.Properties.TryGetValue("class", out var value) && value is string classes)
        {
            foreach (var token in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(token, className, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool MatchesAttribute(TestElement element, string selector)
    {
        if (selector[^1] != ']')
        {
            return false;
        }
        var body = selector[1..^1];
        var equalsIndex = body.IndexOf('=', StringComparison.Ordinal);
        if (equalsIndex < 0)
        {
            return element.Properties.ContainsKey(body.Trim());
        }
        var name = body[..equalsIndex].Trim();
        var value = body[(equalsIndex + 1)..].Trim().Trim('"', '\'');
        return string.Equals(AttributeString(element, name), value, StringComparison.Ordinal);
    }
}
