namespace Assimalign.Viu.Components;

/// <summary>
/// Describes one immutable value in Viu's host-neutral virtual tree.
/// </summary>
/// <remarks>
/// Only this assembly can derive from the type. Extensibility is provided through component
/// invocations, directives, and host bindings rather than renderer-unknown node variants.
/// </remarks>
public abstract class VirtualNode
{
    private protected VirtualNode(
        VirtualNodeKind kind,
        object? key,
        MountReference? mountReference,
        RenderPlan? renderPlan)
    {
        Kind = kind;
        Key = key;
        MountReference = mountReference;
        RenderPlan = renderPlan ?? RenderPlan.None;
    }

    /// <summary>Gets the closed structural variant carried by this value.</summary>
    public VirtualNodeKind Kind { get; }

    /// <summary>Gets the optional identity used while reconciling sibling collections.</summary>
    public object? Key { get; }

    /// <summary>Gets the optional callback that receives the mounted host or exposed value.</summary>
    public MountReference? MountReference { get; }

    /// <summary>Gets compiler-produced selective-patching information.</summary>
    public RenderPlan RenderPlan { get; }
}
