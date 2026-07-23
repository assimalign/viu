using System;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Supplies no application services for direct primitive-tree rendering.</summary>
internal sealed class EmptyServiceProvider : IServiceProvider
{
    internal static EmptyServiceProvider Instance { get; } = new();

    private EmptyServiceProvider()
    {
    }

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return null;
    }
}
