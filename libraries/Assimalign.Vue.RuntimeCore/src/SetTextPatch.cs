namespace Assimalign.Vue.RuntimeCore;

public sealed record SetTextPatch(NodePath Path, string Text) : VirtualDomPatch(Path);
