using System;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools.Client;

internal sealed class ClientFrameScheduler : IDevToolsClientScheduler
{
    public async void Schedule(Action callback)
    {
        // Captures the browser synchronization context and yields a frame between bounded drains.
        await Task.Delay(16);
        callback();
    }
}
