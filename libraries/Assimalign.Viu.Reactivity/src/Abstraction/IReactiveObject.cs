namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The contract implemented by source-generated reactive objects. It is Viu's reflection-free,
/// trimming-safe counterpart to the object returned by Vue 3.5's <c>reactive()</c>.
/// </summary>
public interface IReactiveObject : IReactiveTraversable, IReactiveReadOnly
{
    /// <summary>
    /// Returns the underlying non-reactive object. Generated objects are reactive by identity, so
    /// this is normally the same instance.
    /// </summary>
    /// <returns>The raw object.</returns>
    object ToRaw();

    /// <summary>
    /// Returns the dependency backing <paramref name="propertyName"/>, or
    /// <see langword="null"/> when the property is not reactive.
    /// </summary>
    /// <param name="propertyName">The case-sensitive declared property name.</param>
    /// <returns>The property's dependency cell, or <see langword="null"/>.</returns>
    Dependency? GetDependency(string propertyName);

    /// <summary>Gets whether generated property writes are rejected.</summary>
    new bool IsReadOnly => false;

    bool IReactiveReadOnly.IsReadOnly => IsReadOnly;
}
