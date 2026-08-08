using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Owns the results of one render-once component operation: the rendered tree and the live
/// context, released together on disposal. Hosts consume this operation surface instead of
/// Core's mounted engine internals.
/// </summary>
/// <remarks>Specified by <c>[SSR-10]</c>.</remarks>
public interface IComponentRenderScope : IAsyncDisposable
{
    /// <summary>Gets the fresh immutable rendered subtree, or null when nothing rendered.</summary>
    VirtualNode? Tree { get; }

    /// <summary>
    /// Gets the runtime-implemented context; usable as the parent of a nested render operation.
    /// </summary>
    ComponentContext Context { get; }
}
