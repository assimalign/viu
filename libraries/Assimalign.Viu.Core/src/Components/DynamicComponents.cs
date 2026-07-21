namespace Assimalign.Viu;

/// <summary>
/// The runtime behind Vue 3's <c>&lt;component :is&gt;</c> special element — the C# port of
/// <c>resolveDynamicComponent</c> (<c>packages/runtime-core/src/helpers/resolveAssets.ts</c>,
/// https://vuejs.org/api/built-in-special-elements.html#component). A component definition passes
/// through unchanged; a string resolves against the current instance's app component registry
/// ([V01.01.03.12]) and, when unresolved, is treated as an element tag — exactly upstream's
/// element fallback.
/// </summary>
public static class DynamicComponents
{
    /// <summary>
    /// Resolves the <c>is</c> value of a dynamic component (upstream: <c>resolveDynamicComponent</c>):
    /// an <see cref="IComponent"/> is returned unchanged; a non-empty string is resolved
    /// against the app component registry (raw/camelCase/PascalCase) and, if unregistered, returned
    /// as-is to be used as an element tag; a null or empty value resolves to null (rendered as a
    /// comment placeholder).
    /// </summary>
    /// <param name="source">The <c>is</c> value — a component definition, a name, or null.</param>
    /// <returns>The resolved <see cref="IComponent"/>, the element-tag string, or null.</returns>
    /// <remarks>
    /// Deviates from issue #37's literal "warns in dev" clause per upstream parity: upstream
    /// <c>resolveDynamicComponent</c> calls <c>resolveAsset(COMPONENTS, name, /*warnMissing*/ false)</c>,
    /// so an unresolved name is NOT a warning — element fallback is a normal path (e.g.
    /// <c>:is="'div'"</c>). Warning on every element-tag <c>is</c> would contradict the repo's
    /// upstream-wins rule; the deviation is documented and flagged for review.
    /// </remarks>
    public static object? ResolveDynamicComponent(object? source)
    {
        if (source is IComponent definition)
        {
            return definition;
        }
        if (source is string name && name.Length > 0)
        {
            // resolveAsset with warnMissing=false: registered component, else the string as a tag.
            return ComponentInstance.Current?.AppContext?.ResolveComponent(name) is { } resolved
                ? resolved
                : name;
        }
        return null;
    }

    /// <summary>
    /// Builds the vnode for a <c>&lt;component :is&gt;</c> element (upstream:
    /// <c>createVNode(resolveDynamicComponent(is), props, children)</c>): a component vnode when
    /// <paramref name="source"/> resolves to a definition, an element vnode when it resolves to a
    /// tag, or a comment placeholder when it resolves to null. Because the vnode's
    /// <see cref="VirtualNode.ComponentType"/>/<see cref="VirtualNode.ElementTag"/> changes with
    /// <paramref name="source"/>, a changed <c>is</c> fails the renderer's same-type check and
    /// replaces the old tree (full unmount then mount) — the replace-on-change contract.
    /// </summary>
    /// <param name="source">The <c>is</c> value — a component definition, a name, or null.</param>
    /// <param name="properties">The props for the resolved vnode, or null.</param>
    /// <param name="slots">The slot content when the resolved vnode is a component, or null.</param>
    /// <returns>The dynamic component's vnode.</returns>
    public static VirtualNode DynamicComponent(
        object? source,
        VirtualNodeProperties? properties = null,
        ComponentSlots? slots = null)
    {
        return ResolveDynamicComponent(source) switch
        {
            IComponent definition => slots is null
                ? VirtualNodeFactory.Component(definition, properties)
                : VirtualNodeFactory.Component(definition, properties, slots),
            string tag => VirtualNodeFactory.Element(tag, properties, (VirtualNode?[]?)null),
            _ => VirtualNodeFactory.Comment(),
        };
    }
}
