namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A covariant, strongly typed read-only view of a reactive reference. Reading
/// <see cref="Value"/> establishes the same dependency as the underlying reference, while the
/// contract exposes no write operation. Specified by <c>[RCT-1]</c> and <c>[RCT-4]</c>.
/// </summary>
/// <typeparam name="T">The contained value type.</typeparam>
public interface IReactiveReadOnlyReference<out T> : IReactiveReference
{
    /// <summary>Gets the contained value; the read tracks the ambient subscriber.</summary>
    new T Value { get; }
}
