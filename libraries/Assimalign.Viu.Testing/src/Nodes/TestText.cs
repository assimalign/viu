namespace Assimalign.Viu.Testing;

/// <summary>
/// An in-memory text node (mirrors <c>TestText</c> in <c>@vue/runtime-test</c>).
/// </summary>
public sealed class TestText : TestNode
{
    internal TestText(string text)
    {
        Text = text;
    }

    /// <summary>The text content.</summary>
    public string Text { get; internal set; }

    /// <summary>
    /// Whether this node holds a raw static-markup chunk from <c>insertStaticContent</c> rather
    /// than plain text — the serializer emits it verbatim.
    /// </summary>
    public bool IsStaticContent { get; internal init; }
}
