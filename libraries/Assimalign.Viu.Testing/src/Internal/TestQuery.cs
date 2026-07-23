using System;
using System.Collections.Generic;
using System.Text;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

internal static class TestQuery
{
    internal static bool Matches(TestElement element, string selector)
    {
        if (string.IsNullOrEmpty(selector))
        {
            return false;
        }

        return selector[0] switch
        {
            '#' => string.Equals(
                AttributeString(element, "id"),
                selector[1..],
                StringComparison.Ordinal),
            '.' => HasClass(element, selector[1..]),
            '[' => MatchesAttribute(element, selector),
            _ => string.Equals(element.Tag, selector, StringComparison.Ordinal),
        };
    }

    internal static List<TestElement> DescendantElementsOf(TestElement root)
    {
        List<TestElement> result = [];
        for (int index = 0; index < root.Children.Count; index++)
        {
            CollectElementNodes(root.Children[index], result);
        }

        return result;
    }

    internal static List<TestElement> DescendantElementsOf(
        IReadOnlyList<TestNode> roots)
    {
        List<TestElement> result = [];
        for (int index = 0; index < roots.Count; index++)
        {
            CollectElementNodes(roots[index], result);
        }

        return result;
    }

    internal static List<TestNode> HostNodes(TestElement container)
    {
        return new List<TestNode>(container.Children);
    }

    internal static List<TestNode> HostNodes(
        MountedTemplateNode<TestNode> template)
    {
        TestNode first = template.Subtree.FirstHostNode;
        TestNode last = template.Subtree.LastHostNode;
        TestElement? parent = first.Parent;
        if (parent is null || !ReferenceEquals(parent, last.Parent))
        {
            return [];
        }

        int firstIndex = parent.Children.IndexOf(first);
        int lastIndex = parent.Children.IndexOf(last);
        if (firstIndex < 0 || lastIndex < firstIndex)
        {
            return [];
        }

        List<TestNode> result = new(lastIndex - firstIndex + 1);
        for (int index = firstIndex; index <= lastIndex; index++)
        {
            result.Add(parent.Children[index]);
        }

        return result;
    }

    internal static void AppendText(TestNode node, StringBuilder builder)
    {
        switch (node)
        {
            case TestText text:
                builder.Append(text.Text);
                break;
            case TestElement element:
                for (int index = 0; index < element.Children.Count; index++)
                {
                    AppendText(element.Children[index], builder);
                }

                break;
        }
    }

    private static void CollectElementNodes(
        TestNode node,
        List<TestElement> elements)
    {
        if (node is not TestElement element)
        {
            return;
        }

        elements.Add(element);
        for (int index = 0; index < element.Children.Count; index++)
        {
            CollectElementNodes(element.Children[index], elements);
        }
    }

    private static string? AttributeString(TestElement element, string name)
    {
        return element.Properties.TryGetValue(name, out object? value)
            ? value?.ToString()
            : null;
    }

    private static bool HasClass(TestElement element, string className)
    {
        if (!element.Properties.TryGetValue("class", out object? value)
            || value is not string classes)
        {
            return false;
        }

        string[] tokens = classes.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < tokens.Length; index++)
        {
            if (string.Equals(tokens[index], className, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAttribute(TestElement element, string selector)
    {
        if (selector.Length < 2 || selector[^1] != ']')
        {
            return false;
        }

        string body = selector[1..^1];
        int equalsIndex = body.IndexOf('=', StringComparison.Ordinal);
        if (equalsIndex < 0)
        {
            return element.Properties.ContainsKey(body.Trim());
        }

        string name = body[..equalsIndex].Trim();
        string value = body[(equalsIndex + 1)..].Trim().Trim('"', '\'');
        return string.Equals(
            AttributeString(element, name),
            value,
            StringComparison.Ordinal);
    }
}
