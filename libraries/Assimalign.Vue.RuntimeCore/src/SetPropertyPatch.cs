namespace Assimalign.Vue.RuntimeCore;

public sealed record SetPropertyPatch(NodePath Path, string Name, object? Value) : VirtualDomPatch(Path);
