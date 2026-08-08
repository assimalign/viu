namespace Assimalign.Viu.Testing;

/// <summary>Represents mutable in-memory character data created as a text host node.</summary>
/// <remarks>Specified by <c>[RND-HOST-1]</c> and <c>[CONF-3]</c>.</remarks>
public sealed class TestText : TestNode
{
    internal TestText(string text)
    {
        Text = text;
    }

    /// <summary>Gets the current text content.</summary>
    public string Text { get; internal set; }

    /// <summary>Gets whether this node contains compiler-trusted static markup.</summary>
    public bool IsStaticContent { get; internal init; }
}
