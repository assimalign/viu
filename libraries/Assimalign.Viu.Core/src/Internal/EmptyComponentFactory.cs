using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class EmptyComponentFactory : IComponentFactory
{
    internal static EmptyComponentFactory Instance { get; } = new();

    private EmptyComponentFactory()
    {
    }

    public IComponentTemplate Create(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        throw new InvalidOperationException(
            $"Component type \"{componentType}\" is not registered. Configure an application "
            + "component factory before mounting a template request.");
    }

    public IComponentTemplate Create(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        throw new InvalidOperationException(
            $"Component name \"{name}\" is not registered. Configure an application component "
            + "factory before mounting a template request.");
    }
}
