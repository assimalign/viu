namespace Assimalign.Viu.Browser;

/// <summary>
/// Represents an opaque browser-side node handle owned by the JavaScript interop bridge.
/// </summary>
/// <remarks>Specified by <c>[RND-HOST-3]</c>.</remarks>
public sealed class BrowserNodeHandle
{
    internal BrowserNodeHandle(long identifier)
    {
        Identifier = identifier;
    }

    /// <summary>Gets the bridge-relative handle identifier.</summary>
    public long Identifier { get; }
}
