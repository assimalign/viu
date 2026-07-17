namespace Assimalign.Vue.RuntimeCore;

public sealed class VirtualText : VirtualNode
{
    public VirtualText(string content, string? key = null)
        : base(key)
    {
        Content = content ?? string.Empty;
    }

    public override VirtualNodeKind Kind => VirtualNodeKind.Text;

    public string Content { get; }
}
