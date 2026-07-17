namespace Assimalign.Vue.Reactivity;

/// <summary>
/// A strongly-typed reactive value container. Reading <see cref="Value"/> inside an active
/// subscriber establishes a dependency; writing a changed value notifies subscribers.
/// </summary>
/// <typeparam name="T">The type of the contained value.</typeparam>
public interface IReference<T> : IReference
{
    /// <summary>Gets or sets the contained value. Reads track; changed writes trigger.</summary>
    new T Value { get; set; }
}
