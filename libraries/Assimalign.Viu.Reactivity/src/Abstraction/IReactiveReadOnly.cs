namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Reports whether a reactive value rejects writes. This is the interface-based counterpart of
/// Vue 3.5's read-only reactive flag
/// (https://vuejs.org/api/reactivity-utilities.html#isreadonly).
/// </summary>
public interface IReactiveReadOnly
{
    /// <summary>Gets whether writes are rejected while reads continue to participate in tracking.</summary>
    bool IsReadOnly { get; }
}
