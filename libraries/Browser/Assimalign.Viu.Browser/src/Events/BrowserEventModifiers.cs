using System;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Describes the system-modifier keys pressed when a browser event was dispatched.
/// </summary>
/// <remarks>
/// Browser marshals these flags as one integer so modifier guards do not require per-field
/// JavaScript interop. Specified by <c>[SFC-CG-2]</c> and <c>[V01.01.04.03]</c>.
/// </remarks>
[Flags]
public enum BrowserEventModifiers
{
    /// <summary>No system modifier was pressed.</summary>
    None = 0,

    /// <summary>The Control key was pressed.</summary>
    Control = 1,

    /// <summary>The Shift key was pressed.</summary>
    Shift = 1 << 1,

    /// <summary>The Alt or Option key was pressed.</summary>
    Alt = 1 << 2,

    /// <summary>The Meta, Command, or Windows key was pressed.</summary>
    Meta = 1 << 3,
}
