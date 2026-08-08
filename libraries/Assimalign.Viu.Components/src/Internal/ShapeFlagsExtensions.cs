using System.Runtime.CompilerServices;

namespace Assimalign.Viu.Components;

/// <summary>
/// Allocation-free bitwise predicates over <see cref="ShapeFlags"/> — the named form of the
/// <c>flags &amp; ShapeFlags.X</c> checks the renderer performs on every patch visit. Every
/// predicate is a plain inlineable bitwise check;
/// <see cref="System.Enum.HasFlag(System.Enum)"/> is never used, because it boxes.
/// </summary>
internal static class ShapeFlagsExtensions
{
    /// <param name="flags">The vnode's shape flags.</param>
    extension(ShapeFlags flags)
    {
        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="flags"/> contains any bit of
        /// <paramref name="flag"/>.
        /// </summary>
        /// <param name="flag">The flag (or flag combination) to test for.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(ShapeFlags flag) => (flags & flag) != 0;

        /// <summary>Returns <see langword="true"/> when the vnode is a plain element (<see cref="ShapeFlags.Element"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsElement() => (flags & ShapeFlags.Element) != 0;

        /// <summary>Returns <see langword="true"/> when the vnode is a functional component (<see cref="ShapeFlags.FunctionalComponent"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFunctionalComponent() => (flags & ShapeFlags.FunctionalComponent) != 0;

        /// <summary>Returns <see langword="true"/> when the vnode is a stateful component (<see cref="ShapeFlags.StatefulComponent"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsStatefulComponent() => (flags & ShapeFlags.StatefulComponent) != 0;

        /// <summary>Returns <see langword="true"/> when the vnode is any component — stateful or functional (<see cref="ShapeFlags.Component"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsComponent() => (flags & ShapeFlags.Component) != 0;

        /// <summary>Returns <see langword="true"/> when the vnode's children are a text string (<see cref="ShapeFlags.TextChildren"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasTextChildren() => (flags & ShapeFlags.TextChildren) != 0;

        /// <summary>Returns <see langword="true"/> when the vnode's children are an array of vnodes (<see cref="ShapeFlags.ArrayChildren"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasArrayChildren() => (flags & ShapeFlags.ArrayChildren) != 0;

        /// <summary>Returns <see langword="true"/> when the vnode's children are a slots object (<see cref="ShapeFlags.SlotsChildren"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasSlotsChildren() => (flags & ShapeFlags.SlotsChildren) != 0;

        /// <summary>Returns <see langword="true"/> when the vnode is a <c>&lt;Teleport&gt;</c> built-in (<see cref="ShapeFlags.Teleport"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsTeleport() => (flags & ShapeFlags.Teleport) != 0;

        /// <summary>Returns <see langword="true"/> when the vnode is a <c>&lt;Suspense&gt;</c> built-in (<see cref="ShapeFlags.Suspense"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSuspense() => (flags & ShapeFlags.Suspense) != 0;

        /// <summary>Returns <see langword="true"/> when the component must be cached by <c>&lt;KeepAlive&gt;</c> instead of unmounted (<see cref="ShapeFlags.ComponentShouldKeepAlive"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldKeepAlive() => (flags & ShapeFlags.ComponentShouldKeepAlive) != 0;

        /// <summary>Returns <see langword="true"/> when the component is being re-activated from the <c>&lt;KeepAlive&gt;</c> cache (<see cref="ShapeFlags.ComponentKeptAlive"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsKeptAlive() => (flags & ShapeFlags.ComponentKeptAlive) != 0;
    }
}
