using System;

using Assimalign.Viu.State;

namespace Assimalign.Viu;

internal sealed class ApplicationServiceProvider : IServiceProvider
{
    private readonly IServiceProvider? _services;
    private readonly IStateStoreRegistry _state;

    internal ApplicationServiceProvider(
        IServiceProvider? services,
        IStateStoreRegistry state)
    {
        _services = services;
        _state = state;
    }

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(IStateStoreRegistry)
            ? _state
            : _services?.GetService(serviceType);
    }
}
