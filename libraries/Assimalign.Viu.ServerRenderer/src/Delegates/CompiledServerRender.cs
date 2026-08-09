using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Writes one compiler-produced server render body into a renderer-owned output state.
/// </summary>
/// <param name="state">
/// The request-local output, application, cancellation, teleport, and component-render state.
/// </param>
/// <returns>A task that completes after the compiled body and all of its fallbacks complete.</returns>
/// <remarks>
/// The delegate is a build-time generated contract: it performs no runtime template compilation and
/// is safe for trimming and NativeAOT. Direct writes and virtual-tree fallbacks share the same
/// <see cref="SsrRenderState"/> so escaping, hydration markers, teleports, cancellation, and streaming
/// boundaries remain one protocol. Specified by <c>[SSR-COMPILE-2]</c> and
/// <c>[SSR-COMPILE-4]</c>.
/// </remarks>
public delegate Task CompiledServerRender(SsrRenderState state);
