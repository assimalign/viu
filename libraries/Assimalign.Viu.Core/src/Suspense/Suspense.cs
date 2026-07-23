using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu;

/// <summary>
/// Coordinates asynchronous dependencies in its default slot and renders fallback content until
/// every dependency in the current pending branch settles.
/// </summary>
/// <remarks>
/// This is Viu's host-generic port of Vue 3.5's <c>Suspense</c>:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/components/Suspense.ts.
/// Each activated template owns one independent boundary. The boundary does not use
/// provide/inject or an application service container.
/// </remarks>
public sealed class Suspense : IComponentTemplate, IDisposable
{
    private static readonly IComponentArguments EmptyArguments =
        new ComponentArguments();

    private ComponentContext? _context;

    /// <inheritdoc/>
    public string? Name => "Suspense";

    /// <inheritdoc/>
    public ComponentFlags Flags => ComponentFlags.None;

    /// <summary>Gets the explicit AOT-safe registration for the built-in template.</summary>
    public static ComponentRegistration Registration =>
        new(
            typeof(Suspense),
            static () => new Suspense(),
            "Suspense");

    /// <summary>Creates an immutable request for the built-in boundary.</summary>
    /// <param name="defaultSlot">The pending branch whose asynchronous dependencies are tracked.</param>
    /// <param name="fallbackSlot">The optional branch displayed while dependencies are pending.</param>
    /// <param name="key">The optional identity of the boundary request.</param>
    /// <returns>The new Suspense template request.</returns>
    public static ITemplateComponent CreateComponent(
        ComponentSlot defaultSlot,
        ComponentSlot? fallbackSlot = null,
        object? key = null)
    {
        ArgumentNullException.ThrowIfNull(defaultSlot);
        ComponentSlots slots = new(SlotFlags.Dynamic)
        {
            ["default"] = defaultSlot,
        };
        if (fallbackSlot is not null)
        {
            slots["fallback"] = fallbackSlot;
        }

        return ComponentTree.Template<Suspense>(
            slots: slots,
            key: key);
    }

    /// <inheritdoc/>
    public ComponentRenderer Setup(IComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context as ComponentContext
            ?? throw new InvalidOperationException(
                "Suspense requires Core's mounted component context.");
        ParentBoundary = _context.SuspenseBoundary;
        Boundary = new SuspenseBoundary();
        _context.SuspenseBoundary = Boundary;
        context.Lifecycle.OnBeforeUnmount(Boundary.Dispose);
        return RenderDefault;
    }

    internal SuspenseBoundary Boundary { get; private set; } = null!;

    internal ISuspenseBoundary? ParentBoundary { get; private set; }

    internal IComponent RenderFallback()
    {
        return TryRenderSlot("fallback");
    }

    internal TResult RunWithParentBoundary<TResult>(
        Func<TResult> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ComponentContext context = _context!;
        ISuspenseBoundary? current = context.SuspenseBoundary;
        context.SuspenseBoundary = ParentBoundary;
        try
        {
            return callback();
        }
        finally
        {
            context.SuspenseBoundary = current;
        }
    }

    private IComponent RenderDefault()
    {
        return TryRenderSlot("default");
    }

    private IComponent TryRenderSlot(string name)
    {
        return _context!.Slots.TryGetValue(
            name,
            out ComponentSlot? slot)
                ? slot(EmptyArguments) ?? ComponentTree.Comment()
                : ComponentTree.Comment();
    }

    /// <summary>Stops pending boundary callbacks owned by this activated template.</summary>
    public void Dispose()
    {
        Boundary?.Dispose();
    }
}
