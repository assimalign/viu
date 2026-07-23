using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Creates component templates from explicit registrations and resolves application services.
/// Component activation and service resolution share one object but retain separate methods and
/// lifetime semantics.
/// </summary>
public interface IComponentFactory : IServiceProvider
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

