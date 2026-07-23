using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Exposes the host elements currently owned by a mounted component context.
/// </summary>
/// <remarks>
/// This narrow bridge supports host-specific composition helpers such as browser CSS variables
/// without adding browser concepts to <see cref="IComponentContext"/> or <see cref="IApplication"/>.
/// A context not created by Core, a context queried before its first render, or an unmounted context
/// returns an empty list.
/// </remarks>
public static class ComponentHost
{
    /// <summary>Gets the outermost host elements rendered by a component.</summary>
    /// <typeparam name="TNode">The expected host node type.</typeparam>
    /// <param name="context">The mounted component context.</param>
    /// <returns>The current outermost host elements.</returns>
    public static IReadOnlyList<TNode> GetRootElements<TNode>(
        IComponentContext context)
        where TNode : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is not ComponentContext componentContext
            || componentContext.IsUnmounted
            || componentContext.RootElementResolver is null)
        {
            return Array.Empty<TNode>();
        }

        IReadOnlyList<object> nodes = componentContext.RootElementResolver();
        if (nodes.Count == 0)
        {
            return Array.Empty<TNode>();
        }

        List<TNode> typed = new(nodes.Count);
        for (int index = 0; index < nodes.Count; index++)
        {
            if (nodes[index] is TNode node)
            {
                typed.Add(node);
            }
        }

        return typed.Count == 0
            ? Array.Empty<TNode>()
            : new ReadOnlyCollection<TNode>(typed);
    }

    /// <summary>
    /// Gets the ordered direct keyed children of the component's rendered element or fragment root.
    /// </summary>
    /// <remarks>
    /// Each result carries the first mounted host element below its direct child. Template and
    /// fragment wrappers are traversed; children without a host element are omitted. Calling this
    /// from a before-update callback observes the outgoing tree, while calling it from an updated
    /// callback observes the patched incoming tree.
    /// </remarks>
    /// <typeparam name="TNode">The expected host element type.</typeparam>
    /// <param name="context">The mounted component context.</param>
    /// <returns>The ordered keyed child-to-element snapshots.</returns>
    public static IReadOnlyList<KeyedComponentHostElement<TNode>>
        GetKeyedChildElements<TNode>(IComponentContext context)
        where TNode : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is not ComponentContext componentContext
            || componentContext.IsUnmounted
            || componentContext.KeyedChildElementResolver is null)
        {
            return Array.Empty<KeyedComponentHostElement<TNode>>();
        }

        IReadOnlyList<KeyedComponentHostElementSnapshot> snapshots =
            componentContext.KeyedChildElementResolver();
        if (snapshots.Count == 0)
        {
            return Array.Empty<KeyedComponentHostElement<TNode>>();
        }

        List<KeyedComponentHostElement<TNode>> typed =
            new(snapshots.Count);
        for (int index = 0; index < snapshots.Count; index++)
        {
            KeyedComponentHostElementSnapshot snapshot =
                snapshots[index];
            if (snapshot.Element is TNode element)
            {
                typed.Add(
                    new KeyedComponentHostElement<TNode>(
                        snapshot.Component,
                        snapshot.Key,
                        element));
            }
        }

        return typed.Count == 0
            ? Array.Empty<KeyedComponentHostElement<TNode>>()
            : new ReadOnlyCollection<KeyedComponentHostElement<TNode>>(
                typed);
    }

    /// <summary>
    /// Requests the owning renderer's host commit after a host-specific lifecycle helper has
    /// buffered mutations.
    /// </summary>
    /// <param name="context">The mounted component context.</param>
    public static void QueueHostCommit(IComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is ComponentContext componentContext
            && !componentContext.IsUnmounted)
        {
            componentContext.HostCommitScheduler?.Invoke();
        }
    }
}
