namespace Assimalign.Vue.RuntimeCore.VirtualDom;

public sealed class VFragment : VNode
{
    private static readonly IReadOnlyList<VNode> EmptyChildren = Array.Empty<VNode>();

    public VFragment(IEnumerable<VNode>? children = null, string? key = null)
        : base(key)
    {
        Children = children is null ? EmptyChildren : children.ToArray();
    }

    public override VNodeKind Kind => VNodeKind.Fragment;

    public IReadOnlyList<VNode> Children { get; }
}
