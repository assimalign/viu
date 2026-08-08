using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// Controls optional behavior when an <see cref="IRouterHistory"/> moves between existing entries.
/// The default notifies registered listeners; flags opt out of individual behaviors without relying
/// on positional Boolean arguments. Specified by <c>[RTR-3]</c>.
/// </summary>
[Flags]
public enum RouterHistoryNavigationOptions
{
    /// <summary>Uses the ordinary history behavior, including listener notification.</summary>
    None = 0,

    /// <summary>Moves the history position without notifying registered navigation listeners.</summary>
    SuppressListeners = 1,
}
