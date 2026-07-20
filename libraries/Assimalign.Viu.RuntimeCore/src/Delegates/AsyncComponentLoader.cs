using System.Threading.Tasks;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// Loads an async component's real definition asynchronously — the C# port of upstream's
/// <c>AsyncComponentLoader</c> (<c>packages/runtime-core/src/apiAsyncComponent.ts</c>,
/// https://vuejs.org/guide/components/async.html). Invoked at most once for a successful load: the
/// resolved definition is cached on the wrapper and reused by every later mount without re-invoking
/// the loader, and concurrent mounts of the same async component share one in-flight call. A failed
/// load may be re-invoked through <see cref="AsyncComponentErrorHandler"/> (retry) or on a later
/// mount.
/// <para>
/// The returned task's continuation resumes on the single-threaded WASM synchronization context —
/// do not detach it with <c>ConfigureAwait(false)</c> (it would resume render code off-context).
/// This is the runtime contract only: the loader holds a <b>static</b> reference to the resolved
/// definition (or awaits a supplied one), never a reflection/assembly-download mechanism — true
/// lazy-download of component assemblies is a WASM lazy-loading concern layered on top later.
/// </para>
/// </summary>
/// <returns>A task producing the resolved component definition.</returns>
public delegate Task<IComponentDefinition> AsyncComponentLoader();
