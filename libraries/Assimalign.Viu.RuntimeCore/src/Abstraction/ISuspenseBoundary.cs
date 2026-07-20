using System.Threading.Tasks;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The registration contract an enclosing <c>&lt;Suspense&gt;</c> boundary exposes to a suspensible
/// async component — the seam the C# port of upstream's async/Suspense handshake registers through
/// (<c>packages/runtime-core/src/apiAsyncComponent.ts</c> returning a promise for
/// <c>components/Suspense.ts</c>'s <c>registerDep</c> to await,
/// https://vuejs.org/guide/built-ins/suspense.html). When
/// <see cref="AsyncComponentOptions.Suspensible"/> is set and a boundary is present on the instance,
/// the async component hands the boundary its in-flight load instead of rendering its own
/// loading/error UI, and the boundary drives the fallback until the load settles.
/// <para>
/// The real boundary implementation lands with Suspense ([V01.01.03.20]); this interface exists now
/// so the async-component contract ([V01.01.03.16]) is complete and validated against a fake
/// boundary in tests. Not thread-safe (single-threaded JS event-loop model).
/// </para>
/// </summary>
public interface ISuspenseBoundary
{
    /// <summary>
    /// Registers a suspensible async component's in-flight load with this boundary (upstream: the
    /// async setup's returned promise that <c>Suspense.registerDep</c> awaits). The boundary shows its
    /// fallback until <paramref name="pendingLoad"/> settles, then lets the registered
    /// <paramref name="instance"/> render its resolved subtree.
    /// </summary>
    /// <param name="instance">The async component instance whose load the boundary awaits.</param>
    /// <param name="pendingLoad">The in-flight load task shared across mounts of the async component.</param>
    void RegisterAsyncDependency(ComponentInstance instance, Task pendingLoad);
}
