namespace Assimalign.Vue.RuntimeCore.VirtualDom;

public sealed class VText : VNode
{
    public VText(string content, string? key = null)
        : base(key)
    {
        Content = content ?? string.Empty;
    }

    public override VNodeKind Kind => VNodeKind.Text;

    public string Content { get; }
}
