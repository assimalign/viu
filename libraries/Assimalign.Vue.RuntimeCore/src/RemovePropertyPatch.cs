namespace Assimalign.Vue.RuntimeCore;

public sealed record RemovePropertyPatch(NodePath Path, string Name) : VirtualDomPatch(Path);
