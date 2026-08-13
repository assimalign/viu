using System;

namespace Assimalign.Viu;

/// <summary>Provides a reusable runtime directive as a bundle of delegate hooks.</summary>
/// <remarks>Specified by <c>[CMP-7]</c> and <c>[HYD-4]</c>.</remarks>
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

    /// <summary>Creates the shorthand whose shared hook runs for mounted and updated phases.</summary>
    /// <param name="hook">The shared lifecycle hook.</param>
    /// <returns>The reusable directive.</returns>
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
