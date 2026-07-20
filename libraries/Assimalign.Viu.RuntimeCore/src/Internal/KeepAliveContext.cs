using System;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The renderer internals a mounted <see cref="KeepAlive"/> instance consumes — the C# stand-in for
/// the <c>renderer</c> slice upstream injects onto <c>instance.ctx</c> in <c>mountComponent</c>
/// (<c>packages/runtime-core/src/renderer.ts</c>: <c>if (isKeepAlive(vnode)) instance.ctx.renderer =
/// internals</c>). The renderer creates it on the KeepAlive instance <b>before</b> <c>Setup</c> runs so
/// the component's cache/prune logic can reach back into the platform-generic renderer without
/// <see cref="KeepAlive"/> knowing the <c>TNode</c> type.
/// <para>
/// The split mirrors upstream: <see cref="KeepAlive"/> owns the cache and the render/prune logic; the
/// renderer owns the storage container and the <see cref="Unmount"/> operation. Activation and
/// deactivation stay entirely on the renderer — its shape-flag branches call them directly — so they
/// are not surfaced here. Not thread-safe (single-threaded JS event-loop model).
/// </para>
/// </summary>
internal sealed class KeepAliveContext
{
    /// <summary>
    /// The detached storage element the renderer moves a deactivated subtree into (upstream:
    /// <c>storageContainer = createElement('div')</c>). Boxed <c>TNode</c> — created and read only by
    /// the renderer's deactivate path; opaque to <see cref="KeepAlive"/>.
    /// </summary>
    internal required object StorageContainer { get; init; }

    /// <summary>
    /// Really unmounts a cached child vnode — the renderer resets its keep-alive shape flags and tears
    /// it down with host removal (upstream: KeepAlive's local <c>unmount</c> wrapper around
    /// <c>_unmount(vnode, instance, ..., /*doRemove*/ true)</c>). Consumed by <see cref="KeepAlive"/>'s
    /// cache pruning and its own teardown.
    /// </summary>
    internal required Action<VirtualNode> Unmount { get; init; }
}
