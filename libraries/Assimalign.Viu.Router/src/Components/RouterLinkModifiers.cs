using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// Describes the system-modifier keys held for a <see cref="RouterLinkClickEvent"/>. Hosts combine
/// these flags when adapting their native click event, and <see cref="RouterLink"/> leaves any
/// modified click to the host. Specified by <c>[RTR-4]</c> and <c>[RTR-7]</c>.
/// </summary>
[Flags]
public enum RouterLinkModifiers
{
    /// <summary>No system modifier was held.</summary>
    None = 0,

    /// <summary>The Control key was held.</summary>
    Control = 1,

    /// <summary>The Shift key was held.</summary>
    Shift = 1 << 1,

    /// <summary>The Alt or Option key was held.</summary>
    Alt = 1 << 2,

    /// <summary>The Meta, Command, or Windows key was held.</summary>
    Meta = 1 << 3,
}
