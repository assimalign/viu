namespace Assimalign.Vue.RuntimeCore;

public sealed record InsertChildPatch(NodePath ParentPath, int Index, VirtualNode Child) : VirtualDomPatch(ParentPath);
