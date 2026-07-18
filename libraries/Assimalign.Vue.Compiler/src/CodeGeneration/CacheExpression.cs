namespace Assimalign.Vue.Compiler;

/// <summary>
/// A code-generation cache-slot expression: the value is computed once and stored in the render function's
/// per-instance cache array at <see cref="Index"/>, then reused. The C# port of Vue 3.5's
/// <c>CacheExpression</c> (<c>@vue/compiler-core</c> <c>ast.ts</c>). Produced by <c>v-once</c> (cached
/// subtree) and by cached event handlers.
/// </summary>
public sealed record CacheExpression : SyntaxNode
{
    /// <summary>The slot index in the render function's <c>_cache</c> array.</summary>
    public required int Index { get; init; }

    /// <summary>The value to cache.</summary>
    public required SyntaxNode Value { get; init; }

    /// <summary>Whether the cached value is a vnode requiring block-tracking to be paused (upstream <c>needPauseTracking</c>).</summary>
    public bool NeedPauseTracking { get; init; }

    /// <summary>Whether the cache was produced inside a <c>v-once</c> (upstream <c>inVOnce</c>).</summary>
    public bool InVOnce { get; init; }

    /// <summary>Whether code generation must spread the cached array (upstream <c>needArraySpread</c>).</summary>
    public bool NeedArraySpread { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.JsCacheExpression;
}
