using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Invokes one runtime directive lifecycle phase.</summary>
/// <param name="element">The boxed host element.</param>
/// <param name="binding">The resolved binding and its current/previous values.</param>
/// <param name="component">The current immutable element component.</param>
/// <param name="previousComponent">The previous element component on update, or null.</param>
public delegate void DirectiveHook(
    object element,
    DirectiveBinding binding,
    IElementComponent component,
    IElementComponent? previousComponent);
