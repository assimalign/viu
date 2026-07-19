using System.Runtime.CompilerServices;

namespace Assimalign.Viu.Shared;

/// <summary>
/// Allocation-free bitwise predicates over <see cref="ShapeFlags"/>, mirroring the
/// <c>shapeFlag &amp; ShapeFlags.X</c> checks used throughout the upstream renderer. Every
/// predicate is a plain inlineable bitwise check; <see cref="Enum.HasFlag"/> is never used.
/// </summary>
public static class ShapeFlagsExtensions
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="flags"/> contains any bit of
    /// <paramref name="flag"/>.
    /// </summary>
    /// <param name="flags">The vnode's shape flags.</param>
    /// <param name="flag">The flag (or flag combination) to test for.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Has(this ShapeFlags flags, ShapeFlags flag) => (flags & flag) != 0;

    /// <summary>Returns <see langword="true"/> when the vnode is a plain element (<see cref="ShapeFlags.Element"/>).</summary>
    /// <param name="flags">The vnode's shape flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsElement(this ShapeFlags flags) => (flags & ShapeFlags.Element) != 0;

    /// <summary>Returns <see langword="true"/> when the vnode is a functional component (<see cref="ShapeFlags.FunctionalComponent"/>).</summary>
    /// <param name="flags">The vnode's shape flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFunctionalComponent(this ShapeFlags flags) => (flags & ShapeFlags.FunctionalComponent) != 0;

    /// <summary>Returns <see langword="true"/> when the vnode is a stateful component (<see cref="ShapeFlags.StatefulComponent"/>).</summary>
    /// <param name="flags">The vnode's shape flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStatefulComponent(this ShapeFlags flags) => (flags & ShapeFlags.StatefulComponent) != 0;

    /// <summary>Returns <see langword="true"/> when the vnode is any component — stateful or functional (<see cref="ShapeFlags.Component"/>).</summary>
    /// <param name="flags">The vnode's shape flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsComponent(this ShapeFlags flags) => (flags & ShapeFlags.Component) != 0;

    /// <summary>Returns <see langword="true"/> when the vnode's children are a text string (<see cref="ShapeFlags.TextChildren"/>).</summary>
    /// <param name="flags">The vnode's shape flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasTextChildren(this ShapeFlags flags) => (flags & ShapeFlags.TextChildren) != 0;

    /// <summary>Returns <see langword="true"/> when the vnode's children are an array of vnodes (<see cref="ShapeFlags.ArrayChildren"/>).</summary>
    /// <param name="flags">The vnode's shape flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasArrayChildren(this ShapeFlags flags) => (flags & ShapeFlags.ArrayChildren) != 0;

    /// <summary>Returns <see langword="true"/> when the vnode's children are a slots object (<see cref="ShapeFlags.SlotsChildren"/>).</summary>
    /// <param name="flags">The vnode's shape flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasSlotsChildren(this ShapeFlags flags) => (flags & ShapeFlags.SlotsChildren) != 0;

    /// <summary>Returns <see langword="true"/> when the vnode is a <c>&lt;Teleport&gt;</c> built-in (<see cref="ShapeFlags.Teleport"/>).</summary>
    /// <param name="flags">The vnode's shape flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTeleport(this ShapeFlags flags) => (flags & ShapeFlags.Teleport) != 0;

    /// <summary>Returns <see langword="true"/> when the vnode is a <c>&lt;Suspense&gt;</c> built-in (<see cref="ShapeFlags.Suspense"/>).</summary>
    /// <param name="flags">The vnode's shape flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSuspense(this ShapeFlags flags) => (flags & ShapeFlags.Suspense) != 0;

    /// <summary>Returns <see langword="true"/> when the component must be cached by <c>&lt;KeepAlive&gt;</c> instead of unmounted (<see cref="ShapeFlags.ComponentShouldKeepAlive"/>).</summary>
    /// <param name="flags">The vnode's shape flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldKeepAlive(this ShapeFlags flags) => (flags & ShapeFlags.ComponentShouldKeepAlive) != 0;

    /// <summary>Returns <see langword="true"/> when the component is being re-activated from the <c>&lt;KeepAlive&gt;</c> cache (<see cref="ShapeFlags.ComponentKeptAlive"/>).</summary>
    /// <param name="flags">The vnode's shape flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsKeptAlive(this ShapeFlags flags) => (flags & ShapeFlags.ComponentKeptAlive) != 0;
}
