using System.Collections.Generic;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// The per-render block-tree collection state — the C# port of the block machinery in
/// <c>@vue/runtime-core</c> (<c>openBlock</c>/<c>closeBlock</c>/<c>setupBlock</c>/<c>currentBlock</c>/
/// <c>isBlockTreeEnabled</c> in <c>packages/runtime-core/src/vnode.ts</c>,
/// https://vuejs.org/guide/extras/rendering-mechanism.html#compiler-informed-virtual-dom). An open
/// block accumulates the dynamic descendants created within it — flattened across static depth —
/// into its <see cref="VirtualNode.DynamicChildren"/>, so the renderer patches only those nodes and
/// skips static subtrees. On WASM every skipped patch visit is a skipped JS-interop call.
/// <para>
/// All state is ambient <c>static</c>: the runtime targets the single-threaded JS event loop, so
/// this is <b>not thread-safe</b>. The stack is reused across renders (it never churns), and the
/// growable accumulator lists are drawn from a free-list and returned once a block is closed and
/// its children copied to a right-sized array — the persistent <c>DynamicChildren</c> arrays stay
/// right-sized while collection allocates no per-render lists.
/// </para>
/// </summary>
internal static class BlockStack
{
    // The empty accumulation result: a block with no dynamic descendants shares this (upstream
    // EMPTY_ARR), so a static block allocates nothing.
    private static readonly VirtualNode[] Empty = [];

    // The stack of open blocks; each entry is one block's accumulator, or null for a
    // disable-tracking block (v-once content, upstream openBlock(true)).
    private static readonly List<List<VirtualNode>?> Stack = [];

    // A free-list of accumulator lists: rented on OpenBlock, returned once the block is closed and
    // its children copied out, so collection reuses lists instead of allocating per render.
    private static readonly Stack<List<VirtualNode>> Pool = new();

    // The innermost open block's accumulator, cached from the top of Stack (upstream currentBlock).
    // Null when no block is open or the open block disables tracking.
    private static List<VirtualNode>? current;

    // Upstream isBlockTreeEnabled: > 0 collects dynamic children, <= 0 suspends collection (a
    // v-once expression brackets itself with SetBlockTracking(-1) / SetBlockTracking(1)).
    private static int enabled = 1;

    /// <summary>
    /// Opens a block (upstream: <c>openBlock</c>): pushes a fresh accumulator — or null when
    /// <paramref name="disableTracking"/> is set — and makes it the current block.
    /// </summary>
    /// <param name="disableTracking">True for v-once content whose descendants are never collected.</param>
    internal static void OpenBlock(bool disableTracking)
    {
        var block = disableTracking ? null : Rent();
        Stack.Add(block);
        current = block;
    }

    /// <summary>
    /// Adjusts block-tree tracking (upstream: <c>setBlockTracking</c>). The upstream <c>hasOnce</c>
    /// marking is intentionally omitted: Vuecs unmount always walks the full children rather than
    /// the <c>DynamicChildren</c> fast path, so nested components inside v-once content are always
    /// torn down and the guard it protects is not reachable.
    /// </summary>
    /// <param name="value">-1 suspends collection; +1 resumes it.</param>
    internal static void SetBlockTracking(int value) => enabled += value;

    /// <summary>
    /// Closes the current block and stamps its collected dynamic descendants onto
    /// <paramref name="block"/> (upstream: <c>setupBlock</c>): the block vnode is then itself
    /// registered into the enclosing block, because a block is always patched and so must persist
    /// as a dynamic child of its parent.
    /// </summary>
    /// <param name="block">The block vnode being created.</param>
    /// <returns><paramref name="block"/>.</returns>
    internal static VirtualNode CloseBlockAndSetup(VirtualNode block)
    {
        var accumulator = current;
        // Upstream: dynamicChildren = isBlockTreeEnabled > 0 ? currentBlock || EMPTY_ARR : null.
        block.DynamicChildren = enabled > 0 ? Materialize(accumulator) : null;
        Recycle(accumulator);
        CloseBlock();
        if (enabled > 0 && current is not null)
        {
            current.Add(block);
        }
        return block;
    }

    /// <summary>
    /// Registers a freshly created vnode as a dynamic child of the current block, per upstream
    /// <c>createBaseVNode</c> tracking: a vnode is collected when it needs patching on updates — it
    /// carries a positive patch flag, or it is a component (whose instance must persist across
    /// renders). A vnode whose only flag is <see cref="PatchFlags.NeedHydration"/> is not dynamic
    /// (handler caching), matching upstream.
    /// </summary>
    /// <param name="vnode">The vnode to consider for collection.</param>
    internal static void TrackDynamicChild(VirtualNode vnode)
    {
        if (enabled > 0
            && current is not null
            && ((int)vnode.PatchFlag > 0 || (vnode.ShapeFlag & ShapeFlags.Component) != 0)
            && vnode.PatchFlag != PatchFlags.NeedHydration)
        {
            current.Add(vnode);
        }
    }

    /// <summary>
    /// Test hook: clears the stack, current block, pool, and enable counter so one test's block
    /// state cannot leak into the next (ambient static state is the single-threaded design).
    /// </summary>
    internal static void Reset()
    {
        Stack.Clear();
        Pool.Clear();
        current = null;
        enabled = 1;
    }

    private static void CloseBlock()
    {
        // Upstream closeBlock: pop the stack and restore the parent block as current.
        Stack.RemoveAt(Stack.Count - 1);
        current = Stack.Count > 0 ? Stack[^1] : null;
    }

    private static VirtualNode[] Materialize(List<VirtualNode>? accumulator)
        => accumulator is null || accumulator.Count == 0 ? Empty : accumulator.ToArray();

    private static void Recycle(List<VirtualNode>? accumulator)
    {
        if (accumulator is not null)
        {
            accumulator.Clear();
            Pool.Push(accumulator);
        }
    }

    private static List<VirtualNode> Rent() => Pool.Count > 0 ? Pool.Pop() : [];
}
