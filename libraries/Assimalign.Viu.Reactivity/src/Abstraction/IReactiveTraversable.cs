namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Exposes nested reactive members to deep-watch traversal without reflection. Specified by
/// <c>[RCT-6]</c> and <c>[RCT-7]</c>.
/// </summary>
public interface IReactiveTraversable
{
    /// <summary>
    /// Reads and visits each nested reactive member while an active subscriber is collecting
    /// dependencies.
    /// </summary>
    /// <param name="traversal">The traversal collecting reachable dependencies.</param>
    void Traverse(ReactiveTraversal traversal);
}
