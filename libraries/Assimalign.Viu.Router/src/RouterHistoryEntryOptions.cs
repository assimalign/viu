namespace Assimalign.Viu.Router;

/// <summary>
/// Optional caller-provided values for an entry created or replaced through
/// <see cref="IRouterHistory"/>. Internal adjacency, replacement, and position state remains owned
/// by the history implementation. Specified by <c>[RTR-3]</c>.
/// </summary>
public readonly record struct RouterHistoryEntryOptions
{
    /// <summary>
    /// Gets the scroll anchor to carry on the entry, or <see langword="null"/> when none is supplied.
    /// </summary>
    public ScrollPosition? Scroll { get; init; }
}
