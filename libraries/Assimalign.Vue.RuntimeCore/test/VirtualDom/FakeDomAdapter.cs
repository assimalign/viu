using Assimalign.Vue.RuntimeCore.VirtualDom;

namespace Assimalign.Vue.RuntimeCore.Tests.VirtualDom;

/// <summary>
/// Minimal in-memory adapter for exercising the renderer without a browser. The full
/// test renderer ships with [V01.01.11.01]; this fake only covers restructure smoke tests.
/// </summary>
internal sealed class FakeDomAdapter : IVirtualDomAdapter<FakeNode>
{
    public int DestroyedCount { get; private set; }

    public FakeNode CreateElement(string tagName) => new(tagName, null);

    public FakeNode CreateText(string textContent) => new(null, textContent);

    public FakeNode CreateComment(string textContent) => new("#comment", textContent);

    public void SetText(FakeNode node, string textContent) => node.Text = textContent;

    public void SetProperty(FakeNode node, string name, object? value) => node.Properties[name] = value;

    public void RemoveProperty(FakeNode node, string name) => node.Properties.Remove(name);

    public void AppendChild(FakeNode parent, FakeNode child)
    {
        child.Parent?.Children.Remove(child);
        child.Parent = parent;
        parent.Children.Add(child);
    }

    public void InsertBefore(FakeNode parent, FakeNode child, FakeNode beforeChild)
    {
        child.Parent?.Children.Remove(child);
        child.Parent = parent;
        var index = parent.Children.IndexOf(beforeChild);
        parent.Children.Insert(index < 0 ? parent.Children.Count : index, child);
    }

    public void RemoveChild(FakeNode parent, FakeNode child)
    {
        parent.Children.Remove(child);
        child.Parent = null;
    }

    public void ClearChildren(FakeNode parent)
    {
        foreach (var child in parent.Children)
        {
            child.Parent = null;
        }

        parent.Children.Clear();
    }

    public void DestroyNode(FakeNode node) => DestroyedCount++;
}

internal sealed class FakeNode
{
    public FakeNode(string? tagName, string? text)
    {
        TagName = tagName;
        Text = text;
    }

    public string? TagName { get; }

    public string? Text { get; set; }

    public FakeNode? Parent { get; set; }

    public List<FakeNode> Children { get; } = [];

    public Dictionary<string, object?> Properties { get; } = new(StringComparer.Ordinal);
}
