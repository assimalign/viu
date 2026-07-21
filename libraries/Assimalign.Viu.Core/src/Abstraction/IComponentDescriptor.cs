namespace Assimalign.Viu;

/// <summary>
/// Definition-time metadata registration for a <see cref="Component"/>: the fluent surface a
/// component's <see cref="Component.Configure(IComponentDescriptor)"/> uses to declare its props
/// (upstream: the <c>props</c> option) and emitted events (upstream: the <c>emits</c> option)
/// — <c>packages/runtime-core/src/component.ts</c>, https://vuejs.org/api/options-state.html.
/// </summary>
/// <remarks>
/// Lifecycle hooks are deliberately NOT on this contract for v1 (ADR-0004, composition-only;
/// reshape arc 2 ratified decision 3): the descriptor is definition-time metadata only, and
/// lifecycle registration stays inside <c>Setup</c> via the composition-API hooks. Definition-level
/// hooks would need their own ADR.
/// </remarks>
public interface IComponentDescriptor
{
    /// <summary>
    /// Declares an emitted event (upstream: an entry of the <c>emits</c> option). A declared
    /// event's handler prop is excluded from attribute fallthrough.
    /// </summary>
    /// <param name="emit">The emitted-event definition.</param>
    /// <returns>This descriptor, for chaining.</returns>
    IComponentDescriptor WithEmit(ComponentEmitDefinition emit);

    /// <summary>
    /// Declares a prop (upstream: an entry of the <c>props</c> option) as precomputed metadata —
    /// never discovered via reflection (AOT/trimming contract).
    /// </summary>
    /// <param name="property">The property definition.</param>
    /// <returns>This descriptor, for chaining.</returns>
    IComponentDescriptor WithProperty(ComponentPropertyDefinition property);
}
