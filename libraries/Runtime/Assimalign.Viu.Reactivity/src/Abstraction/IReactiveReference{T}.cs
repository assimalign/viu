namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A strongly typed reference-like reactive value. Reading <see cref="Value"/> inside an active
/// subscriber establishes a dependency; writing a changed value notifies subscribers. Specified
/// by <c>[RCT-1]</c> and <c>[RCT-4]</c>.
/// </summary>
/// <typeparam name="T">The contained value type.</typeparam>
public interface IReactiveReference<T> : IReactiveReadOnlyReference<T>
{
    /// <summary>Gets or sets the contained value. Reads track and changed writes trigger.</summary>
    new T Value { get; set; }
}
