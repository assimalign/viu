using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Handles one renderer lifecycle phase for an immutable virtual node.</summary>
/// <param name="value">The current node.</param>
/// <param name="previousValue">The previous node for update hooks, or null.</param>
/// <remarks>
/// Hooks are carried by reserved <c>onVnode*</c> element bindings and are consumed by Core rather
/// than forwarded to the host binding layer. Specified by <c>[RND-PATCH-7]</c>.
/// </remarks>
public delegate void VirtualNodeLifecycleHook(
    VirtualNode value,
    VirtualNode? previousValue);
