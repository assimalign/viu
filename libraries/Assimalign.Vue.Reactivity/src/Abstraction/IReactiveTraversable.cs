namespace Assimalign.Vue.Reactivity;

/// <summary>
/// Implemented by reactive values that can expose their nested reactive members to a
/// <see cref="ReactiveTraversal"/> — the reflection-free C# stand-in for the object/array/collection
/// walking in Vue 3.5's <c>traverse()</c>
/// (<c>packages/reactivity/src/watch.ts</c>). Source-generated <c>[Reactive]</c> objects
/// (see <see cref="IReactiveObject"/>) and the reactive collection types implement it so a deep
/// <c>watch</c> can subscribe to every dependency reachable from a source without reflecting over
/// fields. Plain CLR objects are not traversable — that is documented behavior, not a silent no-op.
/// </summary>
public interface IReactiveTraversable
{
    /// <summary>
    /// Reads (and thereby tracks) each of this value's reactive members and hands each member to
    /// <paramref name="traversal"/> for further descent. Invoked only while a subscriber is
    /// collecting dependencies, so every read establishes a dependency edge.
    /// </summary>
    /// <param name="traversal">The traversal collecting reachable dependencies.</param>
    void Traverse(ReactiveTraversal traversal);
}
