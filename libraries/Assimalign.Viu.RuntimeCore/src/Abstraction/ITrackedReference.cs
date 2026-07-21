namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Contract for reactive sources that own a <see cref="Reactivity.Dependency"/> directly — the refs
/// (<see cref="Reference{T}"/>, <see cref="ShallowReference{T}"/>, <see cref="CustomReference{T}"/>)
/// and <see cref="Computed{T}"/>. Exposing the underlying dependency lets
/// <see cref="Reactive.TriggerReference"/> force-notify a source without knowing its concrete type,
/// and lets .NET developers reach the dependency graph rooted at a reactive value. The C# port of
/// the <c>dep</c> accessor Vue 3.5 reads through in <c>triggerRef</c>
/// (<c>packages/reactivity/src/ref.ts</c>). Implementations expose it as an explicit interface
/// member so it does not widen the ref's public value-facing surface.
/// </summary>
public interface ITrackedReference
{
    /// <summary>
    /// The dependency that tracks reads of, and is triggered by writes to, this source's value cell.
    /// Reading it never tracks; it is a stable inspection handle for the reactive source.
    /// </summary>
    Dependency Dependency { get; }
}
