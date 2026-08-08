namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A code-generation cache-slot expression: the value is computed once and stored in the render function's
/// per-instance cache array at <see cref="Index"/>, then reused. Produced by <c>v-once</c> (cached
/// subtree) and by cached event handlers. The slot count becomes the generated
/// <c>RenderCacheSize</c> constant (<c>[SFC-CG-1]</c>).
/// </summary>
public sealed record CacheExpression : TemplateSyntaxNode
{
    /// <summary>The slot index in the render function's per-mount <c>ComponentRenderFrame</c>.</summary>
    public required int Index { get; init; }

    /// <summary>The value to cache.</summary>
    public required TemplateSyntaxNode Value { get; init; }

    /// <summary>Whether the cached value is a vnode requiring block-tracking to be paused.</summary>
    public bool NeedPauseTracking { get; init; }

    /// <summary>Whether the cache was produced inside a <c>v-once</c>.</summary>
    public bool InVOnce { get; init; }

    /// <summary>Whether code generation must spread the cached array.</summary>
    public bool NeedArraySpread { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.JsCacheExpression;
}
