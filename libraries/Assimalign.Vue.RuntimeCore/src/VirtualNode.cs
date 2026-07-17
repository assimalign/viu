namespace Assimalign.Vue.RuntimeCore;

public abstract class VirtualNode
{
    protected VirtualNode(string? key)
    {
        Key = key;
    }

    public string? Key { get; }

    public abstract VirtualNodeKind Kind { get; }
}
