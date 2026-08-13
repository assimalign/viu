namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Selects the runtime shape produced by <see cref="RenderFunctionEmitter"/>.
/// </summary>
/// <remarks>
/// Both profiles consume one transformed template at build time. The virtual-node profile is the
/// host-neutral interactive path; the server-markup profile writes provably serializable regions
/// directly and retains the same virtual-node algebra only as an explicit fallback. Specified by
/// <c>[SSR-COMPILE-1]</c> through <c>[SSR-COMPILE-3]</c>.
/// </remarks>
public enum RenderFunctionTargetProfile
{
    /// <summary>Emits the host-neutral virtual-node render body used by interactive renderers.</summary>
    VirtualNodeTree = 0,

    /// <summary>
    /// Emits an asynchronous server-markup body over <c>SsrRenderState</c>, with virtual-node
    /// construction limited to fallback regions.
    /// </summary>
    ServerMarkup = 1,
}
