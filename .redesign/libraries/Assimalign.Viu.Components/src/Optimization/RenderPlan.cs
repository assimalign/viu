using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Carries compiler-produced block and binding information used for selective patching.
/// </summary>
public sealed class RenderPlan
{
    /// <summary>Gets the plan used when no compiler optimization metadata is present.</summary>
    public static RenderPlan None { get; } = new();

    /// <summary>Initializes a render plan.</summary>
    /// <param name="patchFlags">The categories that may change.</param>
    /// <param name="dynamicBindingIndices">The binding indices that may change, or null when unknown.</param>
    /// <param name="dynamicChildren">
    /// The direct dynamic descendants, an empty collection for a static block, or null when this is
    /// not a compatible compiler block.
    /// </param>
    public RenderPlan(
        PatchFlags patchFlags = PatchFlags.None,
        IEnumerable<int>? dynamicBindingIndices = null,
        IEnumerable<VirtualNode>? dynamicChildren = null)
    {
        PatchFlags = patchFlags;
        DynamicBindingIndices = CollectionSnapshot.CopyNullable(dynamicBindingIndices);
        DynamicChildren = CollectionSnapshot.CopyNullable(dynamicChildren);
    }

    /// <summary>Gets the categories that may change.</summary>
    public PatchFlags PatchFlags { get; }

    /// <summary>Gets direct dynamic binding indices, or null when the set is unknown.</summary>
    public IReadOnlyList<int>? DynamicBindingIndices { get; }

    /// <summary>
    /// Gets direct dynamic descendants, an empty collection for a static block, or null when this
    /// node does not carry compatible block metadata.
    /// </summary>
    public IReadOnlyList<VirtualNode>? DynamicChildren { get; }

    /// <summary>Gets whether the plan represents a compiler block, including a static block.</summary>
    public bool IsBlock => DynamicChildren is not null;
}
