using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes a namespaced element, its host bindings, directives, and child structure.
/// </summary>
public sealed class ElementNode : CompositeVirtualNode
{
    /// <summary>Initializes an immutable element node.</summary>
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
        Name = name;
        Bindings = CollectionSnapshot.Copy(bindings);
        Directives = CollectionSnapshot.Copy(directives);
    }

    /// <summary>Gets the qualified element name.</summary>
    public QualifiedName Name { get; }

    /// <summary>Gets the immutable host binding snapshot.</summary>
    public IReadOnlyList<ElementBinding> Bindings { get; }

    /// <summary>Gets the immutable directive snapshot.</summary>
    public IReadOnlyList<DirectiveInvocation> Directives { get; }
}
