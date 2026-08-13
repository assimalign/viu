using System;
using System.Threading;

namespace Assimalign.Viu.State;

/// <summary>
/// Selects request-local ambient store state without changing the shared Browser event-loop default.
/// </summary>
internal static class StateExecutionIsolation
{
    private static readonly AsyncLocal<StateExecutionState?> IsolatedState = new();
    private static readonly StateExecutionState SharedState = new();

    internal static StateExecutionState Current => IsolatedState.Value ?? SharedState;

    internal static IDisposable Enter()
    {
        StateExecutionState? previous = IsolatedState.Value;
        IsolatedState.Value = new StateExecutionState();
        return new IsolationLease(previous);
    }

    private sealed class IsolationLease : IDisposable
    {
        private readonly StateExecutionState? _previous;
        private bool _isDisposed;

        internal IsolationLease(StateExecutionState? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            IsolatedState.Value = _previous;
        }
    }
}
