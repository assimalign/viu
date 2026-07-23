using System;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Resolves browser and Core built-ins before delegating application component requests.
/// </summary>
/// <remarks>
/// This wrapper is intentionally only an <see cref="IComponentFactory"/>. It neither implements
/// <see cref="IServiceProvider"/> nor performs service resolution.
/// </remarks>
internal sealed class BrowserComponentFactory : IComponentFactory
{
    private static readonly ComponentActivator TransitionActivator =
        Transition.Registration.Activator;
    private static readonly ComponentActivator TransitionGroupActivator =
        TransitionGroup.Registration.Activator;
    private static readonly ComponentActivator BaseTransitionActivator =
        BaseTransition.Registration.Activator;

    private readonly IComponentFactory _applicationComponents;

    internal BrowserComponentFactory(
        IComponentFactory applicationComponents)
    {
        ArgumentNullException.ThrowIfNull(applicationComponents);
        _applicationComponents = applicationComponents;
    }

    /// <inheritdoc/>
    public IComponentTemplate Create(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (componentType == typeof(Transition))
        {
            return TransitionActivator();
        }

        if (componentType == typeof(TransitionGroup))
        {
            return TransitionGroupActivator();
        }

        if (componentType == typeof(BaseTransition))
        {
            return BaseTransitionActivator();
        }

        return _applicationComponents.Create(componentType);
    }

    /// <inheritdoc/>
    public IComponentTemplate Create(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (string.Equals(name, "Transition", StringComparison.Ordinal))
        {
            return TransitionActivator();
        }

        if (string.Equals(
            name,
            "TransitionGroup",
            StringComparison.Ordinal))
        {
            return TransitionGroupActivator();
        }

        if (string.Equals(name, "BaseTransition", StringComparison.Ordinal))
        {
            return BaseTransitionActivator();
        }

        return _applicationComponents.Create(name);
    }
}
