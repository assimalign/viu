using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools;

internal sealed class RetryableInitialization
{
    private Task? _initialization;

    internal async Task InitializeAsync(
        Func<CancellationToken, Task> initialize,
        CancellationToken cancellationToken)
    {
        _initialization ??= initialize(cancellationToken);
        Task initialization = _initialization;
        try
        {
            await initialization.ConfigureAwait(false);
        }
        catch
        {
            if (ReferenceEquals(_initialization, initialization))
            {
                _initialization = null;
            }

            throw;
        }
    }
}
