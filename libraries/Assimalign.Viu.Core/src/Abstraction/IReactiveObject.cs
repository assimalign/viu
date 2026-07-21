namespace Assimalign.Viu;

/// <summary>
/// The contract every source-generated <c>[Reactive]</c> object implements — the compiled C#
/// substitute for the object returned by Vue 3.5's <c>reactive()</c>
/// (https://vuejs.org/api/reactivity-core.html#reactive). Because C# has no <c>Proxy</c> and WASM
/// forbids reflection, the <c>[Reactive]</c> source generator emits per-property
/// <see cref="Dependency"/> tracking directly onto the annotated partial class and surfaces these
/// hooks so raw access, per-property dependency introspection, and deep traversal all work without
/// reflection.
/// <para>
/// Parity caveat (documented divergence from <c>reactive()</c>): there is no identity-swapping
/// wrapper — the annotated instance is <em>itself</em> reactive, so <see cref="ToRaw"/> returns the
/// same instance. The reactive member set is fixed at compile time; dynamic property addition is not
/// supported.
/// </para>
/// </summary>
public interface IReactiveObject : IReactiveTraversable
{
    /// <summary>
    /// The underlying non-reactive object — the port of Vue's <c>toRaw()</c>. In Viu the reactive
    /// instance is not a proxy over a separate target, so this returns the instance itself; it exists
    /// so identity-safe lookups and <c>toRaw</c>-style consumers work uniformly.
    /// </summary>
    /// <returns>The raw object (the instance itself).</returns>
    object ToRaw();

    /// <summary>
    /// The <see cref="Dependency"/> backing the named reactive property, or <see langword="null"/>
    /// when no such reactive property exists — enabling <c>toRefs</c>-style projection and tooling
    /// without reflection.
    /// </summary>
    /// <param name="propertyName">The declared property name (case-sensitive, ordinal).</param>
    /// <returns>The property's dependency cell, or <see langword="null"/>.</returns>
    Dependency? GetDependency(string propertyName);
}
