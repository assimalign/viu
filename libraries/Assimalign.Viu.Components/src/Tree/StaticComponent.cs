using System;

namespace Assimalign.Viu.Components;

/// <summary>An immutable pre-rendered static component.</summary>
public sealed class StaticComponent : IStaticComponent
{
    /// <summary>Creates a static component.</summary>
    /// <param name="content">The platform-specific static markup.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    public StaticComponent(
        string content,
        object? key = null,
        ComponentOptimization? optimization = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
        Key = key;
        Optimization = optimization ?? ComponentOptimization.None;
    }

    /// <inheritdoc/>
    public ComponentKind Kind => ComponentKind.Static;

    /// <inheritdoc/>
    public object? Key { get; }

    /// <inheritdoc/>
    public ComponentOptimization Optimization { get; }

    /// <inheritdoc/>
    public string Content { get; }
}
