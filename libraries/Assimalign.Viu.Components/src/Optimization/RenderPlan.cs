using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Carries compiler-produced block and binding information used for selective patching.
/// </summary>
/// <remarks>
/// All collection inputs are copied. A <see langword="null"/> dynamic-child list means no block;
/// an empty list means an optimized block with no dynamic descendants. Specified by
/// <c>[RND-BLOCK-1]</c> through <c>[RND-BLOCK-3]</c>.
/// </remarks>
public sealed class RenderPlan
{
    /// <summary>Gets the plan used when no compiler optimization metadata is present.</summary>
    public static RenderPlan None { get; } = new();

    /// <summary>Initializes a render plan.</summary>
    /// <param name="patchFlags">The categories that may change.</param>
    /// <param name="dynamicBindingIndices">The binding indices that may change, or null when unknown.</param>
    /// <param name="dynamicChildren">
    /// The ordered direct dynamic occurrences, including repeated references, an empty collection
    /// for a static block, or null when this is not a compatible compiler block.
    /// </param>
    public RenderPlan(
        PatchFlags patchFlags = PatchFlags.None,
        IEnumerable<int>? dynamicBindingIndices = null,
        IEnumerable<VirtualNode>? dynamicChildren = null)
    {
        if ((int)patchFlags < 0
            && patchFlags != PatchFlags.Cached
            && patchFlags != PatchFlags.Bail)
        {
            throw new ArgumentOutOfRangeException(nameof(patchFlags));
        }

        PatchFlags = patchFlags;
        DynamicBindingIndices = CollectionSnapshot.CopyNullable(dynamicBindingIndices);
        if (DynamicBindingIndices is not null)
        {
            foreach (int dynamicBindingIndex in DynamicBindingIndices)
            {
                if (dynamicBindingIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(dynamicBindingIndices));
                }
            }
        }

        DynamicChildren = CollectionSnapshot.CopyNullableNonNull(
            dynamicChildren,
            nameof(dynamicChildren));
    }

    /// <summary>Gets the categories that may change.</summary>
    public PatchFlags PatchFlags { get; }

    /// <summary>Gets direct dynamic binding indices, or null when the set is unknown.</summary>
    public IReadOnlyList<int>? DynamicBindingIndices { get; }

    /// <summary>
    /// Gets ordered direct dynamic occurrences, preserving repeated references; an empty collection
    /// denotes a static block and null denotes a node without compatible block metadata.
    /// </summary>
    public IReadOnlyList<VirtualNode>? DynamicChildren { get; }

    /// <summary>Gets whether the plan represents a compiler block, including a static block.</summary>
    public bool IsBlock => DynamicChildren is not null;
}
