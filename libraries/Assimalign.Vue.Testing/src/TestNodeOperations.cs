using System;

using Assimalign.Vue.RuntimeCore;

namespace Assimalign.Vue.Testing;

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
    /// <exception cref="ArgumentNullException"><paramref name="log"/> is null.</exception>
    public static RendererOptions<TestNode> Create(TestNodeOperationLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
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
            PatchProperty = (node, propertyName, previousValue, nextValue, _) =>
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
        };
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
