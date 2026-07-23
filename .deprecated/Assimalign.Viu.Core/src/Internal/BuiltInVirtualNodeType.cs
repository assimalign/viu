namespace Assimalign.Viu;

/// <summary>
/// A built-in vnode-type marker — the C# stand-in for upstream's <c>Fragment</c>/<c>Teleport</c>/
/// <c>Suspense</c>/<c>KeepAlive</c>/<c>BaseTransition</c> symbols from <c>@vue/runtime-core</c>
/// (<c>packages/runtime-core/src/vnode.ts</c> / <c>components/*</c>). The compiled render passes one of
/// these as the <c>tag</c> argument to the vnode factories (e.g. <see cref="RenderHelpers._Fragment"/>);
/// <see cref="RenderHelpers"/> dispatches on the marker identity. <see cref="IsFragment"/> and
/// <see cref="IsTeleport"/> markers are fully realized ([V01.01.03.17] delivered Teleport), and both
/// <c>BaseTransition</c> ([V01.01.04.07]) and <c>KeepAlive</c> ([V01.01.03.18]) are now real components
/// (<see cref="RenderHelpers._BaseTransition"/>/<see cref="RenderHelpers._KeepAlive"/> resolve to
/// <see cref="BaseTransition"/>/<see cref="KeepAlive"/>) rather than markers of this type. The one
/// remaining component-like built-in (Suspense) is still a surface marker whose renderer support is
/// delivered by its own work item, so rendering it throws a clear
/// <see cref="System.NotSupportedException"/> rather than silently mis-rendering.
/// </summary>
internal sealed class BuiltInVirtualNodeType
{
    /// <summary>Creates a built-in marker.</summary>
    /// <param name="name">The upstream built-in name (for diagnostics).</param>
    /// <param name="isFragment">Whether this marker is the fully-realized <c>Fragment</c> type.</param>
    /// <param name="isTeleport">Whether this marker is the <c>Teleport</c> type.</param>
    internal BuiltInVirtualNodeType(string name, bool isFragment, bool isTeleport = false)
    {
        Name = name;
        IsFragment = isFragment;
        IsTeleport = isTeleport;
    }

    /// <summary>The upstream built-in name.</summary>
    internal string Name { get; }

    /// <summary>Whether this marker is the fully-realized <c>Fragment</c> type.</summary>
    internal bool IsFragment { get; }

    /// <summary>Whether this marker is the <c>Teleport</c> type (<see cref="VirtualNodeType.Teleport"/>).</summary>
    internal bool IsTeleport { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
