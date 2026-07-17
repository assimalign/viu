namespace Assimalign.Vue.RuntimeCore.VirtualDom;

public abstract record VirtualDomPatch(NodePath Path);

public sealed record ReplaceNodePatch(NodePath Path, VNode NewNode) : VirtualDomPatch(Path);

public sealed record RemoveNodePatch(NodePath Path) : VirtualDomPatch(Path);

public sealed record SetTextPatch(NodePath Path, string Text) : VirtualDomPatch(Path);

public sealed record SetPropertyPatch(NodePath Path, string Name, object? Value) : VirtualDomPatch(Path);

public sealed record RemovePropertyPatch(NodePath Path, string Name) : VirtualDomPatch(Path);

public sealed record InsertChildPatch(NodePath ParentPath, int Index, VNode Child) : VirtualDomPatch(ParentPath);

public sealed record RemoveChildPatch(NodePath ParentPath, int Index) : VirtualDomPatch(ParentPath);
