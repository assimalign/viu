using System;
using System.Collections.Generic;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// The unified virtual DOM node — the C# port of the vnode interface in
/// <c>@vue/runtime-core</c> (<c>packages/runtime-core/src/vnode.ts</c>). One model represents
/// Element, Component, Text, Comment, Static, and Fragment nodes via <see cref="Type"/> plus the
/// <see cref="ShapeFlag"/> bitmask; <see cref="PatchFlag"/>, <see cref="DynamicProperties"/>, and
/// <see cref="DynamicChildren"/> carry the compiler's patch hints so the renderer can skip work
/// (see https://vuejs.org/guide/extras/rendering-mechanism.html — on WASM every skipped patch
/// visit is a skipped JS-interop call). Instances are created through
/// <see cref="VirtualNodeFactory"/> and are renderer-owned once mounted: <see cref="El"/>,
/// <see cref="Anchor"/>, and <see cref="Component"/> are back-pointers the renderer sets.
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class VirtualNode
{
    internal VirtualNode(VirtualNodeType type)
    {
        Type = type;
    }

    /// <summary>What this vnode represents (upstream: the vnode <c>type</c> union).</summary>
    public VirtualNodeType Type { get; }

    /// <summary>The element tag when <see cref="Type"/> is <see cref="VirtualNodeType.Element"/>.</summary>
    public string? ElementTag { get; internal init; }

    /// <summary>
    /// The component definition when <see cref="Type"/> is <see cref="VirtualNodeType.Component"/>.
    /// Typed as <see cref="object"/> until the component model lands ([V01.01.03.06]).
    /// </summary>
    public object? ComponentType { get; internal init; }

    /// <summary>The vnode's properties, or null when it has none (upstream: <c>props</c>).</summary>
    public VirtualNodeProperties? Properties { get; internal init; }

    /// <summary>
    /// The diffing identity extracted from the <c>"key"</c> prop at creation (upstream:
    /// <c>key</c>). Compared with <see cref="object.Equals(object?, object?)"/> — string and
    /// boxed-number keys both behave like upstream's identity comparison.
    /// </summary>
    public object? Key { get; internal init; }

    /// <summary>
    /// The template-ref binding extracted from the <c>"ref"</c> prop at creation (upstream:
    /// <c>ref</c>). Carried as data for now; the renderer wires refs when the component model
    /// lands ([V01.01.03.06]).
    /// </summary>
    public object? Reference { get; internal init; }

    /// <summary>
    /// The text payload: element text children (<see cref="ShapeFlags.TextChildren"/>),
    /// a Text/Comment node's content, or a Static node's raw markup (upstream: the string arm of
    /// <c>children</c>).
    /// </summary>
    public string? TextChildren { get; internal init; }

    /// <summary>
    /// The child vnodes when <see cref="ShapeFlag"/> has
    /// <see cref="ShapeFlags.ArrayChildren"/> (upstream: the array arm of
    /// <c>children</c>). The renderer normalizes entries in place during mount, mirroring
    /// upstream's write-back in <c>mountChildren</c>.
    /// </summary>
    public VirtualNode[]? ArrayChildren { get; internal init; }

    /// <summary>
    /// The slots object when the vnode is a component with slot children. Typed as
    /// <see cref="object"/> until slots land ([V01.01.03.09]).
    /// </summary>
    public object? SlotChildren { get; internal init; }

    /// <summary>
    /// The node-kind and children-shape bitmask (upstream: <c>shapeFlag</c>); values match
    /// <c>@vue/shared</c> bit-for-bit via <see cref="ShapeFlags"/>.
    /// </summary>
    public ShapeFlags ShapeFlag { get; internal set; }

    /// <summary>
    /// The compiler's patch optimization hint (upstream: <c>patchFlag</c>); values match
    /// <c>@vue/shared</c> via <see cref="PatchFlags"/>. A positive flag promises the
    /// children/props follow the compiled contract; <see cref="PatchFlags.Bail"/> forces
    /// a full diff.
    /// </summary>
    public PatchFlags PatchFlag { get; internal init; }

    /// <summary>
    /// The names of the props that can change, when the compiler stamped
    /// <see cref="PatchFlags.Props"/> (upstream: <c>dynamicProps</c>).
    /// </summary>
    public string[]? DynamicProperties { get; internal init; }

    /// <summary>
    /// The block's dynamic descendants collected by the block tree (upstream:
    /// <c>dynamicChildren</c>). Populated once block-tree fast paths land ([V01.01.03.15]);
    /// the field exists now so compiled vnodes round-trip.
    /// </summary>
    public IList<VirtualNode>? DynamicChildren { get; set; }

    /// <summary>
    /// The platform node this vnode is mounted to (upstream: <c>el</c>). Renderer-owned; the
    /// boxed <c>TNode</c> of the active renderer.
    /// </summary>
    public object? El { get; internal set; }

    /// <summary>
    /// The end anchor for Fragment and Static nodes (upstream: <c>anchor</c>). Renderer-owned.
    /// </summary>
    public object? Anchor { get; internal set; }

    /// <summary>
    /// The mounted component instance (upstream: <c>component</c>). Renderer-owned; typed once
    /// the component model lands ([V01.01.03.06]).
    /// </summary>
    public object? Component { get; internal set; }

    /// <summary>
    /// Looks up a <see cref="VirtualNodeHook"/> prop (e.g. <c>"onVnodeMounted"</c>), or null.
    /// </summary>
    /// <param name="name">The hook prop name.</param>
    internal VirtualNodeHook? GetHook(string name)
        => Properties is not null && Properties.TryGetValue(name, out var value) ? value as VirtualNodeHook : null;
}
