using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// The per-mount rendering surface handed to every <see cref="ComponentRenderer"/> invocation. It
/// owns the render cache and assembles block metadata for <see cref="RenderPlan"/> values —
/// replacing the former ambient static helper state, so a failed render is discarded with its
/// frame and concurrent server renders never share state.
/// </summary>
/// <remarks>
/// Compiled template output calls these members through the frame parameter — no static imports
/// into the authored class, no reserved name prefixes. Hand-written render code may use the same
/// surface, or construct nodes directly and accept full-diff patching
/// (<see cref="RenderPlan.None"/>).
/// </remarks>
public sealed class ComponentRenderFrame
{
    private readonly List<List<VirtualNode>> _blocks = [];

    /// <summary>Initializes a frame with a compiler-sized render cache.</summary>
    /// <param name="cacheSize">The number of per-mount cache slots the compiler requested.</param>
    public ComponentRenderFrame(int cacheSize = 0)
    {
        Cache = cacheSize > 0 ? new object?[cacheSize] : [];
    }

    /// <summary>Gets the per-mount cache slots for hoisted nodes, handlers, and memo entries.</summary>
    public object?[] Cache { get; }

    /// <summary>Begins collecting the dynamic descendants of a compiler block.</summary>
    public void OpenBlock() => _blocks.Add([]);

    /// <summary>Records a dynamic node into the innermost open block.</summary>
    /// <param name="node">The node whose bindings or text may change.</param>
    public void Track(VirtualNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_blocks.Count > 0)
        {
            _blocks[^1].Add(node);
        }
    }

    /// <summary>
    /// Ends the innermost block and returns its collected dynamic descendants for the block
    /// node's <see cref="RenderPlan"/> — empty means a valid static block, per the three-state
    /// block contract.
    /// </summary>
    /// <returns>The direct dynamic descendants collected since the matching open.</returns>
    public IReadOnlyList<VirtualNode> CloseBlock()
    {
        if (_blocks.Count == 0)
        {
            return [];
        }
        List<VirtualNode> block = _blocks[^1];
        _blocks.RemoveAt(_blocks.Count - 1);
        return block;
    }

    /// <summary>Caches an event-handler delegate per mount so re-renders keep listener identity.</summary>
    /// <typeparam name="TDelegate">The handler delegate type.</typeparam>
    /// <param name="slot">The compiler-assigned cache slot.</param>
    /// <param name="handler">The handler created on first render.</param>
    /// <returns>The cached handler for every later render.</returns>
    public TDelegate CacheHandler<TDelegate>(int slot, TDelegate handler)
        where TDelegate : Delegate
    {
        Cache[slot] ??= handler;
        return (TDelegate)Cache[slot]!;
    }
}
