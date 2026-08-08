using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes a namespaced element, its host bindings, directives, and child structure.
/// </summary>
/// <remarks>Specified by <c>[CMP-3]</c>.</remarks>
public sealed class ElementNode : CompositeVirtualNode
{
    /// <summary>Initializes an immutable element node from copied collection snapshots.</summary>
    /// <param name="name">The valid qualified host-element name.</param>
    /// <param name="bindings">The host bindings, or <see langword="null"/> for none.</param>
    /// <param name="children">The child nodes, or <see langword="null"/> for none.</param>
    /// <param name="directives">The directive invocations, or <see langword="null"/> for none.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="mountReference">The optional mounted host-value receiver.</param>
    /// <param name="renderPlan">The compiler plan, or <see langword="null"/> for full diffing.</param>
    public ElementNode(
        QualifiedName name,
        IEnumerable<ElementBinding>? bindings = null,
        IEnumerable<VirtualNode>? children = null,
        IEnumerable<DirectiveInvocation>? directives = null,
        object? key = null,
        MountReference? mountReference = null,
        RenderPlan? renderPlan = null)
        : base(VirtualNodeKind.Element, children, key, mountReference, renderPlan)
    {
        if (string.IsNullOrEmpty(name.LocalName))
        {
            throw new ArgumentException("The element local name cannot be empty.", nameof(name));
        }

        Name = name;
        Bindings = CollectionSnapshot.CopyNonNull(bindings, nameof(bindings));
        Directives = CollectionSnapshot.CopyNonNull(directives, nameof(directives));
    }

    /// <summary>Gets the qualified element name.</summary>
    public QualifiedName Name { get; }

    /// <summary>Gets the immutable host binding snapshot.</summary>
    public IReadOnlyList<ElementBinding> Bindings { get; }

    /// <summary>Gets the immutable directive snapshot.</summary>
    public IReadOnlyList<DirectiveInvocation> Directives { get; }
}
