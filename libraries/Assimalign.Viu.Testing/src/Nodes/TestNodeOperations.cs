using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Builds the host operations used by <see cref="Renderer{TNode}"/> over the in-memory test tree.
/// Every operation mutates the tree and is recorded in a <see cref="TestNodeOperationLog"/>.
/// </summary>
public static class TestNodeOperations
{
    /// <summary>Creates the host-operation set.</summary>
    /// <param name="log">The operation log.</param>
    /// <param name="queryRoots">
    /// Roots searched when Core resolves a string Teleport target.
    /// </param>
    /// <param name="strictRemoval">
    /// Whether removing the same host node twice throws, matching handle-based host behavior.
    /// </param>
    /// <param name="snapshotSemantics">
    /// Whether hydration reads an immutable pre-walk rather than the live test tree.
    /// </param>
    /// <returns>The renderer options.</returns>
    public static RendererOptions<TestNode> Create(
        TestNodeOperationLog log,
        IReadOnlyList<TestElement>? queryRoots = null,
        bool strictRemoval = false,
        bool snapshotSemantics = false)
    {
        ArgumentNullException.ThrowIfNull(log);
        HashSet<TestNode>? removedNodes = strictRemoval
            ? new HashSet<TestNode>(ReferenceEqualityComparer.Instance)
            : null;

        return new RendererOptions<TestNode>
        {
            CreateElement = (tag, elementNamespace) =>
            {
                TestElement element = new(tag, elementNamespace);
                log.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.CreateElement,
                        element,
                        Text: tag));
                return element;
            },
            CreateText = text =>
            {
                TestText textNode = new(text);
                log.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.CreateText,
                        textNode,
                        Text: text));
                return textNode;
            },
            CreateComment = text =>
            {
                TestComment comment = new(text);
                log.Add(
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
                            "Only text and comment nodes carry mutable text.");
                }

                log.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.SetText,
                        node,
                        Text: text));
            },
            Insert = (child, parent, anchor) =>
            {
                TestElement parentElement = (TestElement)parent;
                Detach(child);
                int insertIndex = anchor is null
                    ? -1
                    : parentElement.Children.IndexOf(anchor);
                if (insertIndex < 0)
                {
                    parentElement.Children.Add(child);
                }
                else
                {
                    parentElement.Children.Insert(insertIndex, child);
                }

                child.Parent = parentElement;
                log.Add(
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
                log.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.Remove,
                        child,
                        parent));
            },
            ParentNode = node => node.Parent,
            NextSibling = node =>
            {
                if (node.Parent is null)
                {
                    return null;
                }

                List<TestNode> siblings = node.Parent.Children;
                int index = siblings.IndexOf(node);
                return index >= 0 && index + 1 < siblings.Count
                    ? siblings[index + 1]
                    : null;
            },
            PatchAttribute = (
                node,
                _,
                attributeName,
                previousValue,
                nextValue,
                _) =>
            {
                TestElement element = (TestElement)node;
                if (nextValue is null)
                {
                    element.Properties.Remove(attributeName);
                }
                else
                {
                    element.Properties[attributeName] = nextValue;
                }

                if (TestEventNames.IsListener(attributeName))
                {
                    string eventName = attributeName[2..].ToLowerInvariant();
                    if (nextValue is Delegate listener)
                    {
                        element.EventListeners[eventName] = listener;
                    }
                    else
                    {
                        element.EventListeners.Remove(eventName);
                    }
                }

                log.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.PatchAttribute,
                        node,
                        PropertyName: attributeName,
                        PreviousValue: previousValue,
                        NextValue: nextValue));
            },
            ResolveTeleportTarget = queryRoots is null
                ? null
                : target => ResolveTarget(queryRoots, target),
            SetScopeIdentifier = (node, scopeIdentifier) =>
            {
                TestElement element = (TestElement)node;
                element.Properties[scopeIdentifier] = string.Empty;
                log.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.SetScopeIdentifier,
                        node,
                        PropertyName: scopeIdentifier,
                        NextValue: string.Empty));
            },
            CreateHydrationReader = snapshotSemantics
                ? root => new FrozenTestHydrationReader(root)
                : _ => TestHydrationReader.Instance,
            InsertStaticContent = (content, parent, anchor, _) =>
            {
                TestText staticNode = new(content)
                {
                    IsStaticContent = true,
                };
                TestElement parentElement = (TestElement)parent;
                int insertIndex = anchor is null
                    ? -1
                    : parentElement.Children.IndexOf(anchor);
                if (insertIndex < 0)
                {
                    parentElement.Children.Add(staticNode);
                }
                else
                {
                    parentElement.Children.Insert(insertIndex, staticNode);
                }

                staticNode.Parent = parentElement;
                log.Add(
                    new TestNodeOperation(
                        TestNodeOperationType.InsertStaticContent,
                        staticNode,
                        parentElement,
                        anchor,
                        Text: content));
                return (staticNode, staticNode);
            },
        };
    }

    private static TestNode? ResolveTarget(
        IReadOnlyList<TestElement> roots,
        object target)
    {
        if (target is not string selector)
        {
            return null;
        }

        for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
        {
            TestElement root = roots[rootIndex];
            if (TestQuery.Matches(root, selector))
            {
                return root;
            }

            List<TestElement> descendants =
                TestQuery.DescendantElementsOf(root);
            for (int elementIndex = 0;
                elementIndex < descendants.Count;
                elementIndex++)
            {
                if (TestQuery.Matches(descendants[elementIndex], selector))
                {
                    return descendants[elementIndex];
                }
            }
        }

        return null;
    }

    private static void Detach(TestNode node)
    {
        if (node.Parent is not null)
        {
            node.Parent.Children.Remove(node);
            node.Parent = null;
        }
    }
}
