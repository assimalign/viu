using System;
using System.Threading.Tasks;

namespace Assimalign.Viu;

internal sealed class AsynchronousComponentLoadLease : IDisposable
{
    private Action? _release;

    internal AsynchronousComponentLoadLease(
        Task<AsynchronousComponentTarget> pendingLoad,
        Action? release = null)
    {
        PendingLoad = pendingLoad;
        _release = release;
    }

    internal Task<AsynchronousComponentTarget> PendingLoad { get; }

    public void Dispose()
    {
        Action? release = _release;
        _release = null;
        release?.Invoke();
    }
}
