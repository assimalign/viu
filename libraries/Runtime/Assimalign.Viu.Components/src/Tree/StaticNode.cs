using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes compiler-trusted static markup whose format must be supported by the active host.
/// </summary>
/// <remarks>Static nodes are immutable descriptions specified by <c>[CMP-2]</c> and <c>[CMP-3]</c>.</remarks>
public sealed class StaticNode : VirtualNode
{
    /// <summary>Initializes an immutable static markup node.</summary>
    /// <param name="format">The payload serialization contract.</param>
    /// <param name="content">The non-null compiler-trusted payload.</param>
    public StaticNode(MarkupFormat format, string content)
        : base(VirtualNodeKind.Static, null, null, null)
    {
        if (format is not MarkupFormat.Html and not MarkupFormat.ExtensibleMarkupLanguage)
        {
            throw new ArgumentOutOfRangeException(nameof(format));
        }

        ArgumentNullException.ThrowIfNull(content);
        Format = format;
        Content = content;
    }

    /// <summary>Gets the payload serialization contract.</summary>
    public MarkupFormat Format { get; }

    /// <summary>Gets the compiler-trusted static payload.</summary>
    public string Content { get; }
}
