namespace Assimalign.Viu.Testing;

/// <summary>
/// An in-memory comment node. Comments are load-bearing in a rendered tree: they are the anchors
/// and hydration markers the renderer inserts, so a test host must model them as real nodes.
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
