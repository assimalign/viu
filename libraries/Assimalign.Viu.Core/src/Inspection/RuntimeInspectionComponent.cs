using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// A borrowed, reflection-free view of one live authored component passed to an installed
/// runtime inspection hook.
/// </summary>
/// <remarks>
/// The view is a value type so the enabled renderer path does not allocate a wrapper. Its object
/// references are valid only for the hook call; a diagnostics implementation that needs identity
/// across calls must retain weak references. Specified by <c>[DVT-4]</c> and <c>[DVT-5]</c>.
/// </remarks>
public readonly struct RuntimeInspectionComponent
{
    internal RuntimeInspectionComponent(
        IComponent instance,
        IComponent? parent,
        ComponentContract contract,
        ComponentBindings bindings,
        object? exposedState,
        object? key)
    {
        Instance = instance;
        Parent = parent;
        Contract = contract;
        Bindings = bindings;
        ExposedState = exposedState;
        Key = key;
    }

    /// <summary>Gets the authored instance whose reference identity defines this mount.</summary>
    public IComponent Instance { get; }

    /// <summary>Gets the direct authored parent instance, or null for a component root.</summary>
    public IComponent? Parent { get; }

    /// <summary>Gets the immutable static component contract.</summary>
    public ComponentContract Contract { get; }

    /// <summary>Gets the current contract-resolved parameters and slots.</summary>
    public ComponentBindings Bindings { get; }

    /// <summary>
    /// Gets the latest value explicitly published through <see cref="ComponentContext.Expose"/>,
    /// or null when the component has not published one.
    /// </summary>
    public object? ExposedState { get; }

    /// <summary>Gets the optional sibling reconciliation key from the current invocation.</summary>
    public object? Key { get; }
}
