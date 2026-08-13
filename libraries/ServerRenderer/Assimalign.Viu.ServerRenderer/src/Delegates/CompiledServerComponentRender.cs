using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Writes one activated component through its compiler-produced direct-markup body.
/// </summary>
/// <param name="state">The request-local output and serialization state.</param>
/// <param name="component">The explicitly activated authored component instance.</param>
/// <param name="frame">The mount-owned render frame carrying compiler cache state.</param>
/// <param name="scope">The current component lease used as parent context by fallback subtrees.</param>
/// <returns>A task completing after direct writes and subtree-local fallbacks complete.</returns>
/// <remarks>
/// Generated registrations bind the untyped component parameter to a compiler-known type with a
/// static cast. The runtime never discovers a method or activates a type through reflection. The
/// delegate is not thread-safe and is invoked only within one request's logical event loop.
/// Specified by <c>[SSR-TARGET-2]</c> and <c>[SSR-TARGET-3]</c>.
/// </remarks>
public delegate Task CompiledServerComponentRender(
    SsrRenderState state,
    IComponent component,
    ComponentRenderFrame frame,
    IComponentRenderScope scope);
