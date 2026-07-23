using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Applies test stubs, optionally supplies one caller-owned root template, then delegates all
/// remaining activation to the application-selected factory.
/// </summary>
internal sealed class TestComponentFactory : IComponentFactory
{
    private readonly IComponentTemplate? _root;
    private readonly Type? _rootType;
    private readonly IComponentFactory? _components;
    private readonly IReadOnlyDictionary<Type, ComponentActivator?> _stubs;
    private bool _rootCreated;

    internal TestComponentFactory(
        IComponentFactory? components,
        IReadOnlyDictionary<Type, ComponentActivator?> stubs)
    {
        ArgumentNullException.ThrowIfNull(stubs);
        _components = components;
        _stubs = stubs;
    }

    internal TestComponentFactory(
        IComponentTemplate root,
        IComponentFactory? components,
        IReadOnlyDictionary<Type, ComponentActivator?> stubs)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(stubs);
        _root = root;
        _rootType = root.GetType();
        _components = components;
        _stubs = stubs;
    }

    /// <inheritdoc/>
    public IComponentTemplate Create(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (!_rootCreated && componentType == _rootType)
        {
            _rootCreated = true;
            return _root!;
        }

        if (_stubs.TryGetValue(
            componentType,
            out ComponentActivator? stubActivator))
        {
            if (stubActivator is null)
            {
                return StubComponent.For(componentType);
            }

            return stubActivator()
                ?? throw new InvalidOperationException(
                    $"The stub activator for \"{componentType}\" returned null.");
        }

        if (_components is not null)
        {
            return _components.Create(componentType);
        }

        throw new InvalidOperationException(
            $"Component type \"{componentType}\" is not registered for this test mount. "
            + "Supply ComponentMountOptions.Components for child component activation.");
    }

    /// <inheritdoc/>
    public IComponentTemplate Create(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_components is not null)
        {
            return _components.Create(name);
        }

        throw new InvalidOperationException(
            $"Component name \"{name}\" is not registered for this test mount. "
            + "Supply ComponentMountOptions.Components for child component activation.");
    }
}
