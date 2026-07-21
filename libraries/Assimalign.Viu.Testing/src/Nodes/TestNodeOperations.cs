using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Builds the <see cref="RendererOptions{TNode}"/> over the in-memory test tree — the C# port
/// of <c>nodeOps</c> in <c>@vue/runtime-test</c> (<c>packages/runtime-test/src/nodeOps.ts</c>).
/// Every operation is applied to the tree and appended to the
/// <see cref="TestNodeOperationLog"/>, with no test-only shortcuts into renderer internals, so
/// op sequences observed here are the sequences the browser adapter would receive.
/// </summary>
public static class TestNodeOperations
{
    /// <summary>Creates the node-ops set, recording into <paramref name="log"/>.</summary>
    /// <param name="log">The op log to record into.</param>
    /// <param name="teleportTargetRoots">
    /// The live set of roots the <c>querySelector</c> node-op searches to resolve a <c>&lt;Teleport&gt;</c>
    /// string target (each root and its subtree, using the <c>@vue/test-utils</c> selector subset —
    /// tag/<c>#id</c>/<c>.class</c>/<c>[attr]</c>). Null leaves the renderer without a <c>querySelector</c>
    /// option, so a string Teleport target warns as unsupported — matching a renderer that declares none.
    /// The browser adapter resolves targets through the real DOM <c>querySelector</c>; this registered-root
    /// search is the in-memory stand-in ([V01.01.03.17]).
    /// </param>
    /// <param name="snapshotSemantics">
    /// When true, hydration reads are answered by a <see cref="FrozenTestHydrationReader"/> (an immutable
    /// pre-walk, like the browser's batched snapshot) instead of the live-tree
    /// <see cref="TestHydrationReader"/>, and <c>Remove</c> throws on a double-remove (like the browser
    /// bridge's "unknown DOM handle" on a released handle). Together they surface a walker that re-reads
    /// structure after mutating — the snapshot-safety regression coverage for hydration ([V01.01.07.03]).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="log"/> is null.</exception>
    public static RendererOptions<TestNode> Create(
        TestNodeOperationLog log,
        IReadOnlyList<TestElement>? teleportTargetRoots = null,
        bool snapshotSemantics = false)
    {
        ArgumentNullException.ThrowIfNull(log);
        var removedNodes = snapshotSemantics ? new HashSet<TestNode>(ReferenceEqualityComparer.Instance) : null;
        return new RendererOptions<TestNode>
        {
            CreateElement = (tag, elementNamespace) =>
            {
                var element = new TestElement(tag, elementNamespace);
                log.Add(new TestNodeOperation(TestNodeOperationType.CreateElement, element, Text: tag));
                return element;
            },
            CreateText = text =>
            {
                var textNode = new TestText(text);
                log.Add(new TestNodeOperation(TestNodeOperationType.CreateText, textNode, Text: text));
                return textNode;
            },
            CreateComment = text =>
            {
                var comment = new TestComment(text);
                log.Add(new TestNodeOperation(TestNodeOperationType.CreateComment, comment, Text: text));
                return comment;
            },
            SetText = (node, text) =>
            {
                ((TestText)node).Text = text;
                log.Add(new TestNodeOperation(TestNodeOperationType.SetText, node, Text: text));
            },
            SetElementText = (node, text) =>
            {
                var element = (TestElement)node;
                foreach (var child in element.Children)
                {
                    child.Parent = null;
                }
                element.Children.Clear();
                if (text.Length > 0)
                {
                    var textNode = new TestText(text) { Parent = element };
                    element.Children.Add(textNode);
                }
                log.Add(new TestNodeOperation(TestNodeOperationType.SetElementText, node, Text: text));
            },
            Insert = (child, parent, anchor) =>
            {
                var parentElement = (TestElement)parent;
                Detach(child);
                var insertIndex = anchor is null ? -1 : parentElement.Children.IndexOf(anchor);
                if (insertIndex < 0)
                {
                    parentElement.Children.Add(child);
                }
                else
                {
                    parentElement.Children.Insert(insertIndex, child);
                }
                child.Parent = parentElement;
                log.Add(new TestNodeOperation(TestNodeOperationType.Insert, child, parentElement, anchor));
            },
            Remove = child =>
            {
                // Snapshot-semantics mode mirrors the browser bridge: removing an already-removed node throws
                // ("unknown DOM handle" on a released handle), so a walker that re-reads a stale sibling from
                // an immutable snapshot and double-removes fails loudly instead of looping.
                if (removedNodes is not null && !removedNodes.Add(child))
                {
                    throw new InvalidOperationException(
                        $"Snapshot semantics: node #{child.Identifier} removed twice — the hydration walk "
                        + "re-read a stale sibling from the immutable snapshot after mutating "
                        + "(mirrors the browser bridge's 'unknown DOM handle' on a released handle).");
                }
                var parent = child.Parent;
                Detach(child);
                log.Add(new TestNodeOperation(TestNodeOperationType.Remove, child, parent));
            },
            ParentNode = node => node.Parent,
            NextSibling = node =>
            {
                if (node.Parent is null)
                {
                    return null;
                }
                var siblings = node.Parent.Children;
                var index = siblings.IndexOf(node);
                return index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : null;
            },
            PatchProperty = (node, _, propertyName, previousValue, nextValue, _) =>
            {
                var element = (TestElement)node;
                element.Properties[propertyName] = nextValue;
                if (VirtualNodeFactory.IsEventListenerName(propertyName))
                {
                    // onClick -> "click" (parity with @vue/runtime-test patchProp + triggerEvent).
                    var eventName = propertyName[2..].ToLowerInvariant();
                    if (nextValue is Delegate listener)
                    {
                        element.EventListeners[eventName] = listener;
                    }
                    else
                    {
                        element.EventListeners.Remove(eventName);
                    }
                }
                log.Add(new TestNodeOperation(
                    TestNodeOperationType.PatchProperty,
                    node,
                    PropertyName: propertyName,
                    PreviousValue: previousValue,
                    NextValue: nextValue));
            },
            InsertStaticContent = (content, parent, anchor, _) =>
            {
                // One raw node stands in for the static chunk; the browser adapter's template
                // path lands with [V01.01.04.01].
                var staticNode = new TestText(content) { IsStaticContent = true };
                var parentElement = (TestElement)parent;
                var insertIndex = anchor is null ? -1 : parentElement.Children.IndexOf(anchor);
                if (insertIndex < 0)
                {
                    parentElement.Children.Add(staticNode);
                }
                else
                {
                    parentElement.Children.Insert(insertIndex, staticNode);
                }
                staticNode.Parent = parentElement;
                log.Add(new TestNodeOperation(
                    TestNodeOperationType.InsertStaticContent, staticNode, parentElement, anchor, Text: content));
                return (staticNode, staticNode);
            },
            // The in-memory stand-in for the DOM querySelector node-op (upstream nodeOps.querySelector):
            // the first element in the registered roots' subtrees matching the selector, or null. Only
            // wired when target roots are supplied, so a renderer built without them behaves like one
            // that declares no querySelector option (a string Teleport target then warns as unsupported).
            QuerySelector = teleportTargetRoots is null ? null : selector => ResolveTarget(teleportTargetRoots, selector),
            // The in-memory hydration reader reads the live tree directly (no interop snapshot needed), so
            // one stateless instance serves the hydration root and every teleport target. Snapshot-semantics
            // mode instead builds an immutable FrozenTestHydrationReader per root/target, mirroring the
            // browser's batched one-crossing snapshot ([V01.01.07.03]).
            CreateHydrationReader = snapshotSemantics
                ? container => new FrozenTestHydrationReader(container)
                : _ => TestHydrationReader.Instance,
        };
    }

    private static TestNode? ResolveTarget(IReadOnlyList<TestElement> roots, string selector)
    {
        foreach (var root in roots)
        {
            // A registered root is itself a candidate (a detached target container), then its subtree
            // (an in-tree target such as a rendered <div id="modal">), in document order.
            if (TestQuery.Matches(root, selector))
            {
                return root;
            }
            foreach (var descendant in TestQuery.DescendantElementsOf(root))
            {
                if (TestQuery.Matches(descendant, selector))
                {
                    return descendant;
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
