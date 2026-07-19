namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// A DOM built-in-component surface marker — the RuntimeDom analogue of RuntimeCore's internal
/// <c>BuiltInVirtualNodeType</c> for the built-ins whose renderer support has not landed yet
/// (<c>&lt;Transition&gt;</c> / <c>&lt;TransitionGroup&gt;</c>, upstream <c>@vue/runtime-dom</c>
/// <c>components/Transition.ts</c> / <c>TransitionGroup.ts</c>). The compiled render passes one of these as
/// the vnode <c>tag</c> argument (<see cref="DomRenderHelpers._Transition"/> /
/// <see cref="DomRenderHelpers._TransitionGroup"/>); because the marker is neither an element tag string,
/// a component definition, nor a RuntimeCore built-in, the vnode factory's tag dispatch rejects it with a
/// <see cref="System.NotSupportedException"/> at render time rather than silently rendering nothing. That
/// keeps the markers honest placeholders until the transition system is implemented ([V01.01.04.07]).
/// </summary>
internal sealed class DomBuiltInComponentMarker
{
    /// <summary>Creates a marker for the named DOM built-in.</summary>
    /// <param name="name">The upstream built-in name (for diagnostics).</param>
    internal DomBuiltInComponentMarker(string name) => Name = name;

    /// <summary>The upstream built-in name (e.g. <c>Transition</c>).</summary>
    internal string Name { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
