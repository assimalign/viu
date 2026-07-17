namespace Assimalign.Vue.RuntimeCore;

public sealed record RemoveNodePatch(NodePath Path) : VirtualDomPatch(Path);
