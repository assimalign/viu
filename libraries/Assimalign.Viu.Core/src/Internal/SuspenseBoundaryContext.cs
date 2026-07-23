namespace Assimalign.Viu;

/// <summary>
/// Carries the boundary synchronously mounting its subtree.
/// </summary>
/// <remarks>
/// Ambient and single-threaded. Mounted component contexts carry ordinary boundary traversal; this
/// seam remains available to runtime adapters that establish a boundary around activation.
/// </remarks>
internal static class SuspenseBoundaryContext
{
    internal static ISuspenseBoundary? Current;
}
