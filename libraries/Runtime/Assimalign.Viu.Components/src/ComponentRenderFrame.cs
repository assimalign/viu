using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Assimalign.Viu.Components;

/// <summary>
/// The per-mount rendering surface handed to every <see cref="ComponentRenderer"/> invocation.
/// It owns cache slots, stable handler identity, memo metadata, and nested block assembly.
/// </summary>
/// <remarks>
/// A frame is reused by every render of one mount and is never shared with another mount.
/// Compiled output calls it through the renderer parameter, so block collection has no ambient
/// static state. This type is intentionally not thread-safe: Viu renders a mount on the
/// single-threaded host event loop. Specified by <c>[CMP-8]</c>, <c>[RND-BLOCK-1]</c> through
/// <c>[RND-BLOCK-3]</c>, and <c>[SFC-OPT-1]</c>.
/// </remarks>
public sealed class ComponentRenderFrame
{
    private const int LegacyRenderCacheSize = 64;
    private readonly List<BlockFrame> _blocks = [];
    private readonly bool[] _initializedCacheSlots;
    private readonly MemoEntry?[] _memoEntries;
    private int _blockTrackingDepth = 1;

    /// <summary>Initializes a frame with a compiler-sized render cache.</summary>
    /// <param name="cacheSize">The non-negative number of per-mount cache slots requested.</param>
    public ComponentRenderFrame(int cacheSize = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cacheSize);
        Cache = cacheSize == 0 ? [] : new object?[cacheSize];
        _initializedCacheSlots = cacheSize == 0 ? [] : new bool[cacheSize];
        _memoEntries = cacheSize == 0 ? [] : new MemoEntry?[cacheSize];
    }

    /// <summary>
    /// Initializes a frame from one component's immutable compiler contract.
    /// </summary>
    /// <param name="contract">The non-null contract that owns the cache-size declaration.</param>
    /// <remarks>
    /// Compiler-aware contracts use their exact size, including zero. The 64-slot capacity is
    /// retained only for compatibility contracts created before the compiler supplied cache-size
    /// metadata. Specified by <c>[SFC-OPT-1]</c>.
    /// </remarks>
    public ComponentRenderFrame(ComponentContract contract)
        : this(ResolveCacheSize(contract))
    {
    }

    /// <summary>
    /// Gets the fixed-size per-mount cache used by generated code for cached subtrees and other
    /// compiler-owned values. A cached static subtree retains description identity across renders
    /// and MAY be returned at multiple positions; mounted and host state remains occurrence-local.
    /// Specified by <c>[RND-2]</c>, <c>[RND-4]</c>, and <c>[SFC-OPT-1]</c>.
    /// </summary>
    public object?[] Cache { get; }

    /// <summary>
    /// Begins collecting direct dynamic descendants for a compiler block. A locally disabled
    /// block closes to an empty optimized block while global tracking remains active; only a
    /// suspended global tracking depth closes to <see langword="null"/>.
    /// </summary>
    /// <param name="disableTracking">Whether this block suppresses descendant collection.</param>
    public void OpenBlock(bool disableTracking = false) =>
        _blocks.Add(new BlockFrame(disableTracking));

    /// <summary>
    /// Adjusts the nested tracking depth. Negative values suspend collection and positive values
    /// resume it; balanced nested changes preserve the outer suspension.
    /// </summary>
    /// <param name="value">The signed depth adjustment.</param>
    public void SetBlockTracking(int value)
    {
        _blockTrackingDepth = checked(_blockTrackingDepth + value);
    }

    /// <summary>
    /// Appends one direct dynamic occurrence to the innermost open block when tracking is enabled.
    /// Repeated calls with the same node retain every occurrence in order. Calling this method
    /// without an open block is a harmless no-op.
    /// </summary>
    /// <param name="node">The non-null node whose bindings or text may change.</param>
    public void Track(VirtualNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_blockTrackingDepth > 0 && _blocks.Count > 0)
        {
            _blocks[^1].DynamicChildren?.Add(node);
        }
    }

    /// <summary>
    /// Ends the innermost block and returns an immutable ordered occurrence snapshot, preserving
    /// repeated references. An empty snapshot means an optimized block with no dynamic descendants;
    /// <see langword="null"/> means the global tracking depth was suspended. Specified by
    /// <c>[RND-BLOCK-2]</c> and <c>[RND-BLOCK-3]</c>.
    /// </summary>
    /// <returns>
    /// The block snapshot, or <see langword="null"/> while global tracking is suspended.
    /// </returns>
    public IReadOnlyList<VirtualNode>? CloseBlock()
    {
        if (_blocks.Count == 0)
        {
            throw new InvalidOperationException(
                "A render block cannot close without a matching OpenBlock call.");
        }

        BlockFrame block = _blocks[^1];
        _blocks.RemoveAt(_blocks.Count - 1);
        if (_blockTrackingDepth <= 0)
        {
            return null;
        }

        return block.DynamicChildren is null
            ? Array.Empty<VirtualNode>()
            : new ReadOnlyCollection<VirtualNode>(block.DynamicChildren.ToArray());
    }

    /// <summary>Returns a cached value, creating it once for this mount when absent.</summary>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="slot">The compiler-assigned cache slot.</param>
    /// <param name="factory">The value factory invoked at most once.</param>
    /// <returns>The stable cached value, including a cached <see langword="null"/> value.</returns>
    public TValue GetOrAddCache<TValue>(int slot, Func<TValue> factory)
    {
        ValidateCacheSlot(slot);
        ArgumentNullException.ThrowIfNull(factory);
        if (!_initializedCacheSlots[slot] && Cache[slot] is not null)
        {
            _initializedCacheSlots[slot] = true;
        }

        if (!_initializedCacheSlots[slot])
        {
            Cache[slot] = factory();
            _initializedCacheSlots[slot] = true;
            _memoEntries[slot] = null;
        }

        return Cache[slot] is null
            ? default!
            : (TValue)Cache[slot]!;
    }

    /// <summary>Stores a compiler-owned value in one validated cache slot.</summary>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="slot">The compiler-assigned cache slot.</param>
    /// <param name="value">The value retained for this mount.</param>
    /// <returns><paramref name="value"/>.</returns>
    public TValue SetCache<TValue>(int slot, TValue value)
    {
        ValidateCacheSlot(slot);
        Cache[slot] = value;
        _initializedCacheSlots[slot] = true;
        _memoEntries[slot] = null;
        return value;
    }

    /// <summary>Caches an event-handler delegate so re-renders retain listener identity.</summary>
    /// <typeparam name="TDelegate">The handler delegate type.</typeparam>
    /// <param name="slot">The compiler-assigned cache slot.</param>
    /// <param name="handler">The non-null handler created for the first render.</param>
    /// <returns>The cached handler for every later render.</returns>
    public TDelegate CacheHandler<TDelegate>(int slot, TDelegate handler)
        where TDelegate : Delegate
    {
        ArgumentNullException.ThrowIfNull(handler);
        return GetOrAddCache(slot, () => handler);
    }

    /// <summary>Lazily creates and caches an event handler for stable listener identity.</summary>
    /// <typeparam name="TDelegate">The handler delegate type.</typeparam>
    /// <param name="slot">The compiler-assigned cache slot.</param>
    /// <param name="factory">The non-null handler factory invoked at most once.</param>
    /// <returns>The cached handler for every render of this mount.</returns>
    public TDelegate CacheHandler<TDelegate>(int slot, Func<TDelegate> factory)
        where TDelegate : Delegate
    {
        ArgumentNullException.ThrowIfNull(factory);
        return GetOrAddCache(slot, factory);
    }

    /// <summary>
    /// Reuses a cached subtree while all memo dependencies compare equal, otherwise renders and
    /// stores a replacement. A cache hit is tracked as the current block's dynamic descendant.
    /// </summary>
    /// <param name="slot">The compiler-assigned cache slot.</param>
    /// <param name="dependencies">The ordered dependency values for this render.</param>
    /// <param name="render">The subtree factory invoked on a cache miss.</param>
    /// <returns>The cached or newly rendered subtree.</returns>
    public VirtualNode? Memo(
        int slot,
        IReadOnlyList<object?> dependencies,
        Func<VirtualNode?> render)
    {
        ValidateCacheSlot(slot);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(render);
        if (IsMemoSameCore(slot, dependencies))
        {
            VirtualNode? cached = Cache[slot] as VirtualNode;
            if (cached is not null)
            {
                Track(cached);
            }

            return cached;
        }

        VirtualNode? node = render();
        object?[] snapshot = new object?[dependencies.Count];
        for (int index = 0; index < dependencies.Count; index++)
        {
            snapshot[index] = dependencies[index];
        }

        Cache[slot] = node;
        _initializedCacheSlots[slot] = true;
        _memoEntries[slot] = new MemoEntry(snapshot);
        return node;
    }

    /// <summary>
    /// Tests one memo slot against current dependencies and tracks its cached node on a match.
    /// </summary>
    /// <param name="slot">The compiler-assigned cache slot.</param>
    /// <param name="dependencies">The current ordered dependency values.</param>
    /// <returns>Whether every dependency is unchanged.</returns>
    public bool IsMemoSame(int slot, IReadOnlyList<object?> dependencies)
    {
        ValidateCacheSlot(slot);
        ArgumentNullException.ThrowIfNull(dependencies);
        if (!IsMemoSameCore(slot, dependencies))
        {
            return false;
        }

        if (Cache[slot] is VirtualNode cached)
        {
            Track(cached);
        }

        return true;
    }

    /// <summary>Runtime recovery operation that discards incomplete block assembly after failure.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void ResetBlockTracking()
    {
        _blocks.Clear();
        _blockTrackingDepth = 1;
    }

    private bool IsMemoSameCore(int slot, IReadOnlyList<object?> dependencies)
    {
        MemoEntry? entry = _memoEntries[slot];
        if (entry is null || entry.Dependencies.Length != dependencies.Count)
        {
            return false;
        }

        for (int index = 0; index < dependencies.Count; index++)
        {
            if (!Equals(entry.Dependencies[index], dependencies[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static int ResolveCacheSize(ComponentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return contract.HasCompilerRenderCacheSize
            ? contract.RenderCacheSize
            : LegacyRenderCacheSize;
    }

    private void ValidateCacheSlot(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        if (slot >= Cache.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    private sealed class BlockFrame
    {
        internal BlockFrame(bool disableTracking)
        {
            DynamicChildren = disableTracking ? null : [];
        }

        internal List<VirtualNode>? DynamicChildren { get; }
    }

    private sealed class MemoEntry
    {
        internal MemoEntry(object?[] dependencies)
        {
            Dependencies = dependencies;
        }

        internal object?[] Dependencies { get; }
    }
}
