using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Assimalign.Vue.RuntimeCore;

public sealed class VirtualElement : VirtualNode
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyProperties =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(0, StringComparer.Ordinal));

    private static readonly IReadOnlyList<VirtualNode> EmptyChildren = Array.Empty<VirtualNode>();

    public VirtualElement(
        string tagName,
        IReadOnlyDictionary<string, object?>? properties = null,
        IEnumerable<VirtualNode>? children = null,
        string? key = null)
        : base(key)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException("An element tag name is required.", nameof(tagName));
        }

        TagName = tagName;
        Properties = properties is null
            ? EmptyProperties
            : new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(properties, StringComparer.Ordinal));
        Children = children is null ? EmptyChildren : children.ToArray();
    }

    public override VirtualNodeKind Kind => VirtualNodeKind.Element;

    public string TagName { get; }

    public IReadOnlyDictionary<string, object?> Properties { get; }

    public IReadOnlyList<VirtualNode> Children { get; }
}
