using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// The default component factory. It dispatches component activation through explicit delegates and
/// forwards general service resolution to an externally supplied provider.
/// </summary>
/// <remarks>
/// The factory does not own or dispose the supplied provider. It is not thread-safe.
/// </remarks>
public sealed class ComponentFactory : IComponentFactory
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<Type, ComponentActivator> _componentsByType = new();
    private readonly Dictionary<string, ComponentActivator> _componentsByName =
        new(StringComparer.Ordinal);

    /// <summary>Creates a factory over an external provider and explicit component registrations.</summary>
    /// <param name="services">The external application service provider.</param>
    /// <param name="registrations">The component activation registrations.</param>
    public ComponentFactory(
        IServiceProvider services,
        IEnumerable<ComponentRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registrations);
        _services = services;

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

        return activator(this);
    }

    /// <inheritdoc/>
    public IComponentTemplate Create(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!_componentsByName.TryGetValue(name, out ComponentActivator? activator))
        {
            throw new InvalidOperationException($"Component name \"{name}\" is not registered.");
        }

        return activator(this);
    }

    /// <summary>Creates a fresh template from its explicitly registered generic type.</summary>
    /// <typeparam name="TComponent">The registered component template type.</typeparam>
    /// <returns>A new component template for one mount.</returns>
    public TComponent Create<TComponent>()
        where TComponent : class, IComponentTemplate
    {
        return (TComponent)Create(typeof(TComponent));
    }

    /// <summary>
    /// Resolves an application service. Requests for <see cref="IServiceProvider"/> or
    /// <see cref="IComponentFactory"/> return this factory; all other requests are forwarded.
    /// Component registrations are never treated as service registrations.
    /// </summary>
    /// <param name="serviceType">The requested service type.</param>
    /// <returns>The service, or null when the external provider has no registration.</returns>
    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceType == typeof(IServiceProvider) || serviceType == typeof(IComponentFactory))
        {
            return this;
        }

        return _services.GetService(serviceType);
    }
}
