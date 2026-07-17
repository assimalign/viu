namespace Assimalign.Vue.RuntimeCore;

public sealed record ReplaceNodePatch(NodePath Path, VirtualNode NewNode) : VirtualDomPatch(Path);
