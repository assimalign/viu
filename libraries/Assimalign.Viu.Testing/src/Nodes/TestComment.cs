namespace Assimalign.Viu.Testing;

/// <summary>
/// An in-memory comment node (mirrors <c>TestComment</c> in <c>@vue/runtime-test</c>).
/// </summary>
public sealed class TestComment : TestNode
{
    internal TestComment(string text)
    {
        Text = text;
    }

    /// <summary>The comment content.</summary>
    public string Text { get; internal set; }
}
