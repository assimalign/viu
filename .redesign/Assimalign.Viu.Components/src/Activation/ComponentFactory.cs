using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// The default component factory. It dispatches component activation through explicit delegates.
/// </summary>
/// <remarks>
/// Activators may close over any application-owned resolver. The factory does not own or dispose
/// values captured by activators. It is not thread-safe.
/// </remarks>
public sealed class ComponentFactory : IComponentFactory
{
    private readonly Dictionary<Type, ComponentActivator> _componentsByType = new();
    private readonly Dictionary<string, ComponentActivator> _componentsByName =
        new(StringComparer.Ordinal);

    /// <summary>Creates a factory over explicit component registrations.</summary>
    /// <param name="registrations">The component activation registrations.</param>
    public ComponentFactory(IEnumerable<ComponentRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        foreach (ComponentRegistration registration in registrations)
        {
            if (!_componentsByType.TryAdd(registration.ComponentType, registration.Activator))
            {
                throw new ArgumentException(
                    $"Component type \"{registration.ComponentType}\" is registered more than once.",
                    nameof(registrations));
            }

            if (registration.Name is not null
                && !_componentsByName.TryAdd(registration.Name, registration.Activator))
            {
                throw new ArgumentException(
                    $"Component name \"{registration.Name}\" is registered more than once.",
                    nameof(registrations));
            }
        }
    }

    /// <inheritdoc/>
    public IComponentTemplate Create(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (!_componentsByType.TryGetValue(componentType, out ComponentActivator? activator))
        {
            throw new InvalidOperationException(
                $"Component type \"{componentType}\" is not registered. Register an explicit "
                + "ComponentActivator; runtime constructor discovery is not supported.");
        }

        return activator();
    }

    /// <inheritdoc/>
    public IComponentTemplate Create(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!_componentsByName.TryGetValue(name, out ComponentActivator? activator))
        {
            throw new InvalidOperationException($"Component name \"{name}\" is not registered.");
        }

        return activator();
    }

    /// <summary>Creates a fresh template from its explicitly registered generic type.</summary>
    /// <typeparam name="TComponent">The registered component template type.</typeparam>
    /// <returns>A new component template for one mount.</returns>
    public TComponent Create<TComponent>()
        where TComponent : class, IComponentTemplate
    {
        return (TComponent)Create(typeof(TComponent));
    }

}
