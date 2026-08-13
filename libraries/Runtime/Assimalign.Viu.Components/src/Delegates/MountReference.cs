namespace Assimalign.Viu.Components;

/// <summary>
/// Receives the host node or component-exposed value selected for one virtual-node reference.
/// </summary>
/// <remarks>Specified by <c>[CMP-7]</c> and <c>[RND-BLOCK-7]</c>.</remarks>
/// <param name="value">The current referenced value, or <see langword="null"/> after release.</param>
public delegate void MountReference(object? value);
