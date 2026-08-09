using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Builds the production renderer contract over an in-memory host tree.</summary>
/// <remarks>
/// Every operation mutates the tree and records the same cold-path observation for assertions.
/// Specified by <c>[RND-HOST-1]</c> through <c>[RND-HOST-4]</c> and <c>[HYD-2]</c>.
/// </remarks>
public static class TestNodeOperations
{
    /// <summary>Creates a complete test-host renderer option set.</summary>
    /// <param name="operationLog">The operation log receiving all host writes and commits.</param>
    /// <param name="queryRoots">Optional roots used for teleport selector resolution.</param>
    /// <param name="options">Named host behavior, or <see langword="null"/> for live hydration and ordinary removal.</param>
    /// <returns>The complete renderer option set.</returns>
    public static RendererOptions<TestNode> Create(
        TestNodeOperationLog operationLog,
        IReadOnlyList<TestElement>? queryRoots = null,
        TestRendererOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(operationLog);
        options ??= new TestRendererOptions();
        HashSet<TestNode>? removedNodes = options.StrictRemoval
            ? new HashSet<TestNode>(ReferenceEqualityComparer.Instance)
            : null;

        return new RendererOptions<TestNode>
        {
            CreateElement = name =>
            {
                TestElement element = new(name);
                operationLog.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.CreateElement,
                        element,
                        Text: name.ToString()));
                return element;
            },
            CreateText = text =>
            {
                TestText textNode = new(text);
                operationLog.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.CreateText,
                        textNode,
                        Text: text));
                return textNode;
            },
            CreateComment = text =>
            {
                TestComment comment = new(text);
                operationLog.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.CreateComment,
                        comment,
                        Text: text));
                return comment;
            },
            SetText = (node, text) =>
            {
                switch (node)
                {
                    case TestText textNode:
                        textNode.Text = text;
                        break;
                    case TestComment comment:
                        comment.Text = text;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Only text and comment nodes carry mutable character data.");
                }

                operationLog.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.SetText,
                        node,
                        Text: text));
            },
            Insert = (child, parent, anchor) =>
            {
                TestElement parentElement = RequireElement(parent, "insert parent");
                if (anchor is not null && !ReferenceEquals(anchor.Parent, parentElement))
                {
                    throw new InvalidOperationException(
                        "An insertion anchor must belong to the destination parent.");
                }

                if (ReferenceEquals(child, anchor))
                {
                    return;
                }

                Detach(child);
                int insertIndex = anchor is null
                    ? parentElement.Children.Count
                    : parentElement.IndexOfChild(anchor);
                parentElement.InsertChild(insertIndex, child);
                child.Parent = parentElement;
                operationLog.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.Insert,
                        child,
                        parentElement,
                        anchor));
            },
            Remove = child =>
            {
                if (removedNodes is not null && !removedNodes.Add(child))
                {
                    throw new InvalidOperationException(
                        $"Node #{child.Identifier} was removed more than once.");
                }

                TestElement? parent = child.Parent;
                Detach(child);
                operationLog.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.Remove,
                        child,
                        parent));
            },
            ParentNode = static node => node.Parent,
            NextSibling = static node => NextSibling(node),
            PatchAttribute = (node, previousBinding, nextBinding) =>
            {
                TestElement element = RequireElement(node, "binding target");
                PatchBinding(element, previousBinding, nextBinding);
                ElementBinding binding = nextBinding ?? previousBinding
                    ?? throw new InvalidOperationException(
                        "A binding patch must carry a previous or next binding.");
                operationLog.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.PatchAttribute,
                        node,
                        PropertyName: binding.Name.ToString(),
                        PreviousValue: previousBinding?.Value,
                        NextValue: nextBinding?.Value));
            },
            ResolveTeleportTarget = queryRoots is null
                ? null
                : selector => ResolveTarget(queryRoots, selector),
            Commit = () => operationLog.Add(
                new TestNodeOperation(TestNodeOperationType.Commit)),
            InsertStaticContent = (format, content, parent, anchor) =>
            {
                _ = format;
                TestElement parentElement = RequireElement(parent, "static-content parent");
                TestText staticNode = new(content)
                {
                    IsStaticContent = true,
                };
                int insertIndex = anchor is null
                    ? parentElement.Children.Count
                    : parentElement.IndexOfChild(anchor);
                if (insertIndex < 0)
                {
                    insertIndex = parentElement.Children.Count;
                }

                parentElement.InsertChild(insertIndex, staticNode);
                staticNode.Parent = parentElement;
                operationLog.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.InsertStaticContent,
                        staticNode,
                        parentElement,
                        anchor,
                        Text: content));
                return (staticNode, staticNode);
            },
            CreateHydrationReader = options.SnapshotSemantics
                ? root => new FrozenTestHydrationReader(root)
                : _ => TestHydrationReader.Instance,
            ScheduleHydrationTrigger = options.HydrationTriggers is null
                ? null
                : options.HydrationTriggers.Schedule,
        };
    }

    private static void PatchBinding(
        TestElement element,
        ElementBinding? previousBinding,
        ElementBinding? nextBinding)
    {
        if (previousBinding is not null)
        {
            string previousName = previousBinding.Name.ToString();
            if (nextBinding is null
                || nextBinding.Name != previousBinding.Name
                || nextBinding.Kind != previousBinding.Kind)
            {
                element.RemoveProperty(previousName);
                if (previousBinding.Kind == ElementBindingKind.Event)
                {
                    element.RemoveEventListener(previousBinding.Name.LocalName);
                }
            }
        }

        if (nextBinding is null)
        {
            return;
        }

        string nextName = nextBinding.Name.ToString();
        if (nextBinding.Value is null)
        {
            element.RemoveProperty(nextName);
        }
        else
        {
            element.SetProperty(nextName, nextBinding.Value);
        }

        if (nextBinding.Kind != ElementBindingKind.Event)
        {
            return;
        }

        if (nextBinding.Value is Delegate listener)
        {
            element.SetEventListener(
                TestEventName.Parse(nextBinding.Name.LocalName),
                listener);
        }
        else
        {
            element.RemoveEventListener(nextBinding.Name.LocalName);
        }
    }

    private static TestNode? ResolveTarget(
        IReadOnlyList<TestElement> roots,
        string selector)
    {
        for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
        {
            TestElement root = roots[rootIndex];
            if (TestQuery.Matches(root, selector))
            {
                return root;
            }

            List<TestElement> descendants = TestQuery.DescendantElementsOf(root);
            for (int index = 0; index < descendants.Count; index++)
            {
                if (TestQuery.Matches(descendants[index], selector))
                {
                    return descendants[index];
                }
            }
        }

        return null;
    }

    private static TestElement RequireElement(TestNode node, string role) =>
        node as TestElement
        ?? throw new InvalidOperationException($"The {role} must be a TestElement.");

    private static TestNode? NextSibling(TestNode node)
    {
        TestElement? parent = node.Parent;
        if (parent is null)
        {
            return null;
        }

        int index = parent.IndexOfChild(node);
        return index >= 0 && index + 1 < parent.Children.Count
            ? parent.Children[index + 1]
            : null;
    }

    private static void Detach(TestNode node)
    {
        if (node.Parent is not TestElement parent)
        {
            return;
        }

        parent.RemoveChild(node);
        node.Parent = null;
    }
}
