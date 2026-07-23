using System;

namespace Assimalign.Viu;

/// <summary>A reusable runtime directive expressed as a bundle of delegate hooks.</summary>
public sealed record Directive : IDirective
{
    /// <inheritdoc/>
    public DirectiveHook? Created { get; init; }

    /// <inheritdoc/>
    public DirectiveHook? BeforeMount { get; init; }

    /// <inheritdoc/>
    public DirectiveHook? Mounted { get; init; }

    /// <inheritdoc/>
    public DirectiveHook? BeforeUpdate { get; init; }

    /// <inheritdoc/>
    public DirectiveHook? Updated { get; init; }

    /// <inheritdoc/>
    public DirectiveHook? BeforeUnmount { get; init; }

    /// <inheritdoc/>
    public DirectiveHook? Unmounted { get; init; }

    /// <summary>Creates the function-directive shorthand used for mounted and updated.</summary>
    /// <param name="hook">The shared hook.</param>
    /// <returns>The directive.</returns>
    public static Directive FromFunction(DirectiveHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return new Directive
        {
            Mounted = hook,
            Updated = hook,
        };
    }
}
