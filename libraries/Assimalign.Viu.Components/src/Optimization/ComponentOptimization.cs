using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Components;

/// <summary>
/// Carries the compiler-to-runtime hints that make block-tree patching possible without a separate
/// virtual-node model.
/// </summary>
/// <remarks>
/// This type is the block-tree contract between compiled output and the renderer: what may change
/// (<see cref="PatchFlags"/>), which properties may change, which descendants of a block root are
/// dynamic, and whether suspended block tracking (<c>v-once</c>) occurred inside the block.
/// <para>
/// <see cref="DynamicChildren"/> is three-state and the distinction is normative: <see langword="null"/>
/// means the value is not a block and its children are walked in full; a non-null but <b>empty</b>
/// list means an optimized block with no dynamic descendants, so the renderer skips every child
/// visit; a non-null non-empty list is patched directly. Confusing the null and empty cases is the
/// single most consequential error a producer of this metadata can make. Direct patching is
/// attempted only while the old and new roots agree on block shape; otherwise the renderer falls
/// back to a full child diff. Specified by <c>[RND-BLOCK-1]</c> through <c>[RND-BLOCK-4]</c>.
/// </para>
/// </remarks>
public sealed class ComponentOptimization
{
    /// <summary>Gets the metadata used by hand-authored, unoptimized tree values.</summary>
    public static ComponentOptimization None { get; } = new();

    /// <summary>
    /// Compiler-generated code only; not part of the supported Viu API.
    /// Creates optimization metadata.
    /// </summary>
    /// <param name="patchFlags">The compiler-produced patch flags.</param>
    /// <param name="dynamicProperties">
    /// The property names that may change when <paramref name="patchFlags"/> contains
    /// <see cref="PatchFlags.Props"/>.
    /// </param>
    /// <param name="dynamicChildren">
    /// The dynamic descendants collected for a block root, or null when this value is not a block.
    /// </param>
    /// <param name="hasOnce">
    /// Whether suspended block tracking such as <c>v-once</c> occurred inside the block.
    /// </param>
    [System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
    public ComponentOptimization(
        PatchFlags patchFlags = default,
        IReadOnlyList<string>? dynamicProperties = null,
        IReadOnlyList<IComponent>? dynamicChildren = null,
        bool hasOnce = false)
    {
        PatchFlags = patchFlags;
        DynamicProperties = CopyDynamicProperties(dynamicProperties);
        DynamicChildren = CopyDynamicChildren(dynamicChildren);
        HasOnce = hasOnce;
    }

    /// <summary>Gets the compiler-produced patch flags.</summary>
    public PatchFlags PatchFlags { get; }

    /// <summary>Gets the property names that can change, or null when a selective property patch is unavailable.</summary>
    public IReadOnlyList<string>? DynamicProperties { get; }

    /// <summary>Gets the dynamic descendants of a block root, or null when this value is not a block.</summary>
    public IReadOnlyList<IComponent>? DynamicChildren { get; }

    /// <summary>Gets whether suspended block tracking occurred inside this block.</summary>
    public bool HasOnce { get; }

    /// <summary>Gets whether the value is a block root, including a block with no dynamic descendants.</summary>
    public bool IsBlock => DynamicChildren is not null;

    private static IReadOnlyList<string>? CopyDynamicProperties(
        IReadOnlyList<string>? dynamicProperties)
    {
        if (dynamicProperties is null)
        {
            return null;
        }

        if (dynamicProperties.Count == 0)
        {
            return Array.Empty<string>();
        }

        string[] snapshot = new string[dynamicProperties.Count];
        for (int index = 0; index < dynamicProperties.Count; index++)
        {
            string property = dynamicProperties[index];
            ArgumentException.ThrowIfNullOrEmpty(property);
            snapshot[index] = property;
        }

        return new ReadOnlyCollection<string>(snapshot);
    }

    private static IReadOnlyList<IComponent>? CopyDynamicChildren(
        IReadOnlyList<IComponent>? dynamicChildren)
    {
        if (dynamicChildren is null)
        {
            return null;
        }

        if (dynamicChildren.Count == 0)
        {
            return Array.Empty<IComponent>();
        }

        IComponent[] snapshot = new IComponent[dynamicChildren.Count];
        for (int index = 0; index < dynamicChildren.Count; index++)
        {
            IComponent component = dynamicChildren[index];
            ArgumentNullException.ThrowIfNull(component);
            snapshot[index] = component;
        }

        return new ReadOnlyCollection<IComponent>(snapshot);
    }
}
