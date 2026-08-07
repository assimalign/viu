namespace Assimalign.Viu.Components;

/// <summary>
/// Receives the host node or component-exposed value selected for one virtual-node reference.
/// </summary>
/// <param name="value">The current referenced value, or <see langword="null"/> after release.</param>
public delegate void MountReference(object? value);
