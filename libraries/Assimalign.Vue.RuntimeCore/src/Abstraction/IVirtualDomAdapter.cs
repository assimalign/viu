namespace Assimalign.Vue.RuntimeCore;

public interface IVirtualDomAdapter<TNode>
{
    TNode CreateElement(string tagName);

    TNode CreateText(string textContent);

    TNode CreateComment(string textContent);

    void SetText(TNode node, string textContent);

    void SetProperty(TNode node, string name, object? value);

    void RemoveProperty(TNode node, string name);

    void AppendChild(TNode parent, TNode child);

    void InsertBefore(TNode parent, TNode child, TNode beforeChild);

    void RemoveChild(TNode parent, TNode child);

    void ClearChildren(TNode parent);

    void DestroyNode(TNode node);
}
