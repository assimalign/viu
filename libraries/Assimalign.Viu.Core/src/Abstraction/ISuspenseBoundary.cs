using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Receives pending loads from suspensible asynchronous components in its mounted subtree.
/// </summary>
/// <remarks>
/// This is the host-neutral handshake used by Core's <see cref="Suspense"/> built-in. The boundary
/// owns fallback presentation and coordinated reveal; asynchronous wrappers retain responsibility
/// for loader cancellation and error routing. The contract does not resolve services or use
/// provide/inject.
/// </remarks>
public interface ISuspenseBoundary
{
    /// <summary>
    /// Registers one pending asynchronous-component dependency until it settles or its consumer
    /// unmounts.
    /// </summary>
    /// <param name="component">The mounted asynchronous wrapper context that owns the dependency.</param>
    /// <param name="pendingLoad">The shared in-flight target load.</param>
    void RegisterAsynchronousDependency(
        IComponentContext component,
        Task pendingLoad);
}
