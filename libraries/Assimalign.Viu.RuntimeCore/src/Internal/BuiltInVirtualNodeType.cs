namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// A built-in vnode-type marker — the C# stand-in for upstream's <c>Fragment</c>/<c>Teleport</c>/
/// <c>Suspense</c>/<c>KeepAlive</c>/<c>BaseTransition</c> symbols from <c>@vue/runtime-core</c>
/// (<c>packages/runtime-core/src/vnode.ts</c> / <c>components/*</c>). The compiled render passes one of
/// these as the <c>tag</c> argument to the vnode factories (e.g. <see cref="RenderHelpers._Fragment"/>);
/// <see cref="RenderHelpers"/> dispatches on the marker identity. <see cref="IsFragment"/> markers are fully
/// realized, and <c>BaseTransition</c> is now a real component (<see cref="RenderHelpers._BaseTransition"/>
/// resolves to <see cref="BaseTransition"/>, [V01.01.04.07]) rather than a marker of this type. The
/// remaining component-like built-ins (Teleport, Suspense, KeepAlive) are still surface markers whose
/// renderer support is delivered by their own work items, so rendering one throws a clear
/// <see cref="System.NotSupportedException"/> rather than silently mis-rendering.
/// </summary>
internal sealed class BuiltInVirtualNodeType
{
    /// <summary>Creates a built-in marker.</summary>
    /// <param name="name">The upstream built-in name (for diagnostics).</param>
    /// <param name="isFragment">Whether this marker is the fully-realized <c>Fragment</c> type.</param>
    internal BuiltInVirtualNodeType(string name, bool isFragment)
    {
        Name = name;
        IsFragment = isFragment;
    }

    /// <summary>The upstream built-in name.</summary>
    internal string Name { get; }

    /// <summary>Whether this marker is the fully-realized <c>Fragment</c> type.</summary>
    internal bool IsFragment { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
