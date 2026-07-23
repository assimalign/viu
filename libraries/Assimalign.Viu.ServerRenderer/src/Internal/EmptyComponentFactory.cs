using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Supplies the component resolver for direct primitive-tree rendering, where no template activation
/// is expected.
/// </summary>
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
            $"Component type \"{componentType}\" cannot be activated while rendering a raw component "
            + "tree. Render a ServerApplication configured with an IComponentFactory.");
    }

    public IComponentTemplate Create(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        throw new InvalidOperationException(
            $"Component name \"{name}\" cannot be activated while rendering a raw component tree. "
            + "Render a ServerApplication configured with an IComponentFactory.");
    }
}
