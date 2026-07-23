namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A strongly typed ref-like reactive value. Reading <see cref="Value"/> inside an active
/// subscriber establishes a dependency; writing a changed value notifies subscribers.
/// </summary>
/// <typeparam name="T">The contained value type.</typeparam>
public interface IReactiveReference<T> : IReactiveReference
{
    /// <summary>Gets or sets the contained value. Reads track and changed writes trigger.</summary>
    new T Value { get; set; }
}
