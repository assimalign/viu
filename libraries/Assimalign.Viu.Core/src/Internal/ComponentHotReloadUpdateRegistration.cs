using System;

namespace Assimalign.Viu;

internal sealed class ComponentHotReloadUpdateRegistration : IDisposable
{
    private Action<ComponentHotReloadChangeKind>? _handler;

    internal ComponentHotReloadUpdateRegistration(
        Type componentType,
        Action<ComponentHotReloadChangeKind> handler)
    {
        ComponentType = componentType;
        _handler = handler;
    }

    internal Type ComponentType { get; }

    internal void Invoke(ComponentHotReloadChangeKind change) => _handler?.Invoke(change);

    public void Dispose()
    {
        if (_handler is null)
        {
            return;
        }

        _handler = null;
        ComponentHotReload.UnregisterUpdateHandler(this);
    }
}
