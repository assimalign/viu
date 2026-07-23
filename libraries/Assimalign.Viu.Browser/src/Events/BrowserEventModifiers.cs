using System;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The system-modifier state of a dispatched DOM event, extracted JS-side and marshaled as one
/// integer (W3C UI Events <c>ctrlKey</c>/<c>shiftKey</c>/<c>altKey</c>/<c>metaKey</c>,
/// https://www.w3.org/TR/uievents/). Consumed by <see cref="BrowserEvents"/>.<c>WithModifiers</c>'s
/// system-modifier and <c>.exact</c> guards.
/// </summary>
[Flags]
public enum BrowserEventModifiers
{
    /// <summary>No system modifier was pressed.</summary>
    None = 0,

    /// <summary>The Control key was pressed.</summary>
    Control = 1,

    /// <summary>The Shift key was pressed.</summary>
    Shift = 1 << 1,

    /// <summary>The Alt (Option) key was pressed.</summary>
    Alt = 1 << 2,

    /// <summary>The Meta (Command/Windows) key was pressed.</summary>
    Meta = 1 << 3,
}
