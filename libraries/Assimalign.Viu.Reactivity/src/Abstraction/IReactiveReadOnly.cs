namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Reports whether a reactive value rejects writes. Implementing this interface is what makes a
/// value answer <see cref="Reactive.IsReadonly"/> without a type test per concrete kind.
/// </summary>
public interface IReactiveReadOnly
{
    /// <summary>Gets whether writes are rejected while reads continue to participate in tracking.</summary>
    bool IsReadOnly { get; }
}
