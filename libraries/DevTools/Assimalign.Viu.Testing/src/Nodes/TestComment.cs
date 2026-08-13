namespace Assimalign.Viu.Testing;

/// <summary>Represents an in-memory comment, including structural hydration markers.</summary>
/// <remarks>Specified by <c>[SSR-MARKERS-1]</c>, <c>[HYD-1]</c>, and <c>[CONF-3]</c>.</remarks>
public sealed class TestComment : TestNode
{
    internal TestComment(string text)
    {
        Text = text;
    }

    /// <summary>Gets the current comment data without comment delimiters.</summary>
    public string Text { get; internal set; }
}
