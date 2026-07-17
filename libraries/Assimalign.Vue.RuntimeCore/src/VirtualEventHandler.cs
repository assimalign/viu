using System;

namespace Assimalign.Vue.RuntimeCore;

public sealed class VirtualEventHandler
{
    private readonly Action _callback;

    public VirtualEventHandler(Action callback)
    {
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    public void Invoke()
    {
        _callback();
    }
}
