using System;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// A directive expressed as a bundle of delegate hooks — the C# port of writing an upstream
/// <c>ObjectDirective</c> as an object literal of hook functions
/// (<c>packages/runtime-core/src/directives.ts</c>,
/// https://vuejs.org/guide/reusability/custom-directives.html). Set only the hooks the directive
/// needs; the rest stay null and are skipped. Use <see cref="FromFunction"/> for the shorthand
/// where one function is both the <see cref="IDirective.Mounted"/> and <see cref="IDirective.Updated"/>
/// hook (upstream's function-directive form). Allocation-light — a single reusable object shared
/// across every use of the directive.
/// </summary>
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

    /// <summary>
    /// Creates a directive whose single <paramref name="hook"/> is invoked on both
    /// <see cref="IDirective.Mounted"/> and <see cref="IDirective.Updated"/> (upstream: a
    /// <c>FunctionDirective</c> normalizes to <c>{ mounted: fn, updated: fn }</c>).
    /// </summary>
    /// <param name="hook">The hook run on mount and update.</param>
    /// <returns>The directive.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public static Directive FromFunction(DirectiveHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return new Directive { Mounted = hook, Updated = hook };
    }
}
