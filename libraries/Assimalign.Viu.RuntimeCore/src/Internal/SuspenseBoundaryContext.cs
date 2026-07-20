namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The ambient enclosing <c>&lt;Suspense&gt;</c> boundary a newly created
/// <see cref="ComponentInstance"/> inherits when it has no parent boundary — the injection seam a
/// future Suspense ([V01.01.03.20]) sets while mounting its subtree so descendants pick it up
/// (mirroring how upstream seeds <c>instance.suspense</c> from the rendering context), and the seam
/// the fake boundary uses in async-component tests. Null in production today (no Suspense yet).
/// Ambient static, single-threaded — NOT thread-safe.
/// </summary>
internal static class SuspenseBoundaryContext
{
    /// <summary>The boundary currently mounting its subtree, or null when there is none.</summary>
    internal static ISuspenseBoundary? Current;
}
