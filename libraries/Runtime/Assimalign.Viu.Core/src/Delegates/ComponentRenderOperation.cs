using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Executes one host-specific operation against an activated, prefetched component.
/// </summary>
/// <param name="component">The explicitly activated authored component instance.</param>
/// <param name="frame">The operation-owned render frame carrying compiler cache state.</param>
/// <param name="scope">
/// The live component scope, available as a parent only for the duration of the operation.
/// </param>
/// <returns>A value task completing after the host-specific operation completes.</returns>
/// <remarks>
/// Core retains activation, error routing, and teardown ownership while a host consumes only the
/// public component surfaces needed for direct rendering. Specified by <c>[SSR-10]</c> and
/// <c>[SSR-TARGET-3]</c>.
/// </remarks>
public delegate ValueTask ComponentRenderOperation(
    IComponent component,
    ComponentRenderFrame frame,
    IComponentRenderScope scope);
