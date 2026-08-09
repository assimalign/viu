using System;

namespace Assimalign.Viu.Testing;

internal sealed class TestEventListener
{
    internal TestEventListener(Delegate listener, bool once)
    {
        Listener = listener;
        Once = once;
    }

    internal Delegate Listener { get; }

    internal bool Once { get; }
}
