using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// The explicit, reflection-free unit of component availability: a reference, its static
/// contract, and the activator delegate that produces one authored instance per mount.
/// </summary>
/// <remarks>Specified by <c>[CMP-1]</c> and <c>[CMP-4]</c>.</remarks>
public sealed class ComponentRegistration
{
    /// <summary>
    /// Defines a code-first component from a name, its contract, and a composition setup body.
    /// This is the delegate-based form of the same <see cref="IComponent.Setup"/> contract. Composition-only:
    /// the setup closure is the whole authoring model; there is no configuration-object form.
    /// </summary>
    /// <param name="name">The registered name component nodes reference.</param>
    /// <param name="contract">The static input/output declaration.</param>
    /// <param name="setup">The per-mount composition body returning the render closure.</param>
    /// <returns>A registration whose activator wraps the setup delegate — no reflection.</returns>
    /// <remarks>
    /// Hand-built render output carries <see cref="RenderPlan.None"/> unless the author supplies
    /// plans, so code-first subtrees patch by full diff — correct, just without
    /// compiler-informed skipping.
    /// Specified by <c>[CMP-34]</c> and <c>[RND-BLOCK-1]</c>.
    /// </remarks>
    public static ComponentRegistration Define(string name, ComponentContract contract, ComponentSetup setup)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(setup);
        return new ComponentRegistration(
            ComponentReference.ForName(name),
            contract,
            _ => new DelegateComponent(setup));
    }

    /// <summary>Initializes a component registration.</summary>
    /// <param name="reference">The type or name identity component nodes carry.</param>
    /// <param name="contract">The static input/output declaration.</param>
    /// <param name="activator">The delegate producing one authored instance per mount.</param>
    public ComponentRegistration(
        ComponentReference reference,
        ComponentContract contract,
        ComponentActivator activator)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(activator);
        Reference = reference;
        Contract = contract;
        Activator = activator;
    }

    /// <summary>Gets the type or name identity component nodes carry.</summary>
    public ComponentReference Reference { get; }

    /// <summary>Gets the static input/output declaration.</summary>
    public ComponentContract Contract { get; }

    /// <summary>Gets the delegate producing one authored instance per mount.</summary>
    public ComponentActivator Activator { get; }
}
