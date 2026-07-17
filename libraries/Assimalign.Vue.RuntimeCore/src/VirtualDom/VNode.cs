namespace Assimalign.Vue.RuntimeCore.VirtualDom;

public abstract class VNode
{
    protected VNode(string? key)
    {
        Key = key;
    }

    public string? Key { get; }

    public abstract VNodeKind Kind { get; }
}
