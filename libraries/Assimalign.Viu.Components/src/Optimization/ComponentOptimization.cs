using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Components;

/// <summary>
/// Carries the compiler-to-runtime hints that make block-tree patching possible without a separate
/// virtual-node model.
/// </summary>
/// <remarks>
/// Mirrors Vue 3.5's vnode optimization fields and block collection contract:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/vnode.ts.
/// <para>
/// A non-null <see cref="DynamicChildren"/> value marks a block root, including an empty list.
/// The renderer may patch that list directly when the old and new roots preserve the block shape.
/// </para>
/// </remarks>
public sealed class ComponentOptimization
{
    /// <summary>Gets the metadata used by hand-authored, unoptimized tree values.</summary>
    public static ComponentOptimization None { get; } = new();

    /// <summary>Creates optimization metadata.</summary>
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
