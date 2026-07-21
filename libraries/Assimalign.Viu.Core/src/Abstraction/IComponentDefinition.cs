using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// A Viu component — the C# port of the component options + Composition API <c>setup()</c>
/// contract (<c>packages/runtime-core/src/component.ts</c>,
/// https://vuejs.org/api/composition-api-setup.html). C# has no <c>Proxy</c>, so there is no
/// <c>this</c>-proxy: <see cref="Setup"/> runs exactly once per instance and returns the
/// render function, with state held as refs/computeds the render function closes over — the
/// closure IS the proxy-free realization of upstream's "state object" form. Definitions are
/// plain objects instantiated by user code or source-generated factories — never activated
/// reflectively (AOT/trimming contract).
/// </summary>
public interface IComponentDefinition
{
    /// <summary>The component's display name for warnings and devtools, or null.</summary>
    string? Name => null;

    /// <summary>
    /// The declared props (upstream: the <c>props</c> option), as precomputed metadata —
    /// emitted by the source generator or written explicitly, never discovered via reflection.
    /// Null declares no props: every vnode prop falls through as an attribute.
    /// </summary>
    IReadOnlyList<ComponentPropertyDefinition>? Properties => null;

    /// <summary>
    /// The declared emitted events (upstream: the <c>emits</c> option). Declared events'
    /// handler props are excluded from attribute fallthrough.
    /// </summary>
    IReadOnlyList<ComponentEmitDefinition>? Emits => null;

    /// <summary>
    /// Whether undeclared attributes fall through to a single element root (upstream:
    /// <c>inheritAttrs</c>, default true).
    /// </summary>
    bool InheritAttributes => true;

    /// <summary>
    /// The Composition API entry point (upstream: <c>setup(props, context)</c>): runs once per
    /// instance with the instance current, and returns the render function that re-executes
    /// per update.
    /// </summary>
    /// <param name="properties">The instance's shallow-reactive props.</param>
    /// <param name="context">Attrs, Emit, Expose, and Slots.</param>
    /// <returns>The render function producing the component's subtree.</returns>
    Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context);
}
