using System;

namespace Assimalign.Viu;

internal sealed class SchedulerTimeProviderRegistration : IDisposable
{
    private readonly SchedulerExecutionState _state;
    private readonly TimeProvider _previous;
    private bool _isDisposed;

    internal SchedulerTimeProviderRegistration(SchedulerExecutionState state, TimeProvider current)
    {
        _state = state;
        _previous = state.TimeProvider;
        state.TimeProvider = current;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _state.TimeProvider = _previous;
        }
    }
}
