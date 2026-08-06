using System;

namespace Assimalign.Viu;

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
