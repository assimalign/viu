using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Resolves component templates for mounting.
/// </summary>
/// <remarks>
/// The contract intentionally does not prescribe how components are activated. An application may
/// use explicit delegates, generated activators, a dependency-injection container, or another
/// resolver without making service resolution part of this interface.
/// </remarks>
public interface IComponentFactory
{
    /// <summary>Creates a fresh template from its explicitly registered type.</summary>
    /// <param name="componentType">The registered component template type.</param>
    /// <returns>A new component template for one mount.</returns>
    IComponentTemplate Create(Type componentType);

    /// <summary>Creates a fresh template from its explicitly registered name.</summary>
    /// <param name="name">The registered component name.</param>
    /// <returns>A new component template for one mount.</returns>
    IComponentTemplate Create(string name);

    /// <summary>Creates a fresh template from its explicitly registered generic type.</summary>
    /// <typeparam name="TComponent">The registered component template type.</typeparam>
    /// <returns>A new component template for one mount.</returns>
    TComponent Create<TComponent>()
        where TComponent : class, IComponentTemplate
    {
        return (TComponent)Create(typeof(TComponent));
    }
}
