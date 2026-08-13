using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// The host-free factory facade for the pure in-memory history used by tests and non-browser hosts.
/// Browser web and hash histories are supplied by the downstream Browser.Router integration.
/// Specified by <c>[RTR-3]</c> and <c>[RTR-10]</c>.
/// </summary>
public static class RouterHistory
{
    /// <summary>
    /// Creates an in-memory history — pure, interop-free, and
    /// the mode used for tests and non-browser hosts.
    /// </summary>
    /// <param name="basePath">The base path, or <see langword="null"/> for none.</param>
    public static IRouterHistory CreateMemory(string? basePath = null)
        => new MemoryRouterHistory(basePath);

}
