using System.Collections.ObjectModel;

namespace Assimalign.Vue.RuntimeCore.VirtualDom;

public sealed class VElement : VNode
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyProperties =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(0, StringComparer.Ordinal));

    private static readonly IReadOnlyList<VNode> EmptyChildren = Array.Empty<VNode>();

    public VElement(
        string tagName,
        IReadOnlyDictionary<string, object?>? properties = null,
        IEnumerable<VNode>? children = null,
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

    public override VNodeKind Kind => VNodeKind.Element;

    public string TagName { get; }

    public IReadOnlyDictionary<string, object?> Properties { get; }

    public IReadOnlyList<VNode> Children { get; }
}
