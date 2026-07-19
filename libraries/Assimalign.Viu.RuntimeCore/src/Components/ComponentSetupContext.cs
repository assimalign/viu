using System;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The second argument to <see cref="IComponentDefinition.Setup"/> — the C# port of upstream's
/// <c>SetupContext</c> (<c>packages/runtime-core/src/component.ts</c>,
/// https://vuejs.org/api/composition-api-setup.html#setup-context): the live fallthrough
/// attributes, the emit function, <see cref="Expose"/>, and the slots object (typed once
/// [V01.01.03.09] lands).
/// </summary>
public sealed class ComponentSetupContext
{
    private readonly ComponentInstance _instance;

    internal ComponentSetupContext(ComponentInstance instance)
    {
        _instance = instance;
    }

    /// <summary>The live fallthrough attributes (upstream: <c>context.attrs</c>).</summary>
    public ComponentAttributes Attributes => _instance.Attributes;

    /// <summary>
    /// The slots passed by the parent (upstream: <c>context.slots</c>), rendered through
    /// <see cref="VirtualNodeFactory.RenderSlot"/>; null when the parent passed no slot content.
    /// </summary>
    public ComponentSlots? Slots => _instance.Slots;

    /// <summary>
    /// Emits a component event to the matching handler prop (upstream: <c>context.emit</c>;
    /// see https://vuejs.org/guide/components/events.html).
    /// </summary>
    /// <param name="eventName">The event name as declared (e.g. <c>"change"</c>, <c>"update:modelValue"</c>).</param>
    /// <param name="arguments">The event payload.</param>
    public void Emit(string eventName, params object?[] arguments)
        => _instance.EmitEvent(eventName, arguments);

    /// <summary>
    /// Restricts what a parent template ref sees on this instance (upstream:
    /// <c>context.expose</c>): after exposing, <see cref="ComponentInstance.Exposed"/> is the
    /// only surfaced state.
    /// </summary>
    /// <param name="exposed">The object to surface, or null to expose nothing.</param>
    public void Expose(object? exposed) => _instance.SetExposed(exposed);
}
