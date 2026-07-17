namespace Assimalign.Vue.RuntimeCore;

public sealed record RemoveChildPatch(NodePath ParentPath, int Index) : VirtualDomPatch(ParentPath);
