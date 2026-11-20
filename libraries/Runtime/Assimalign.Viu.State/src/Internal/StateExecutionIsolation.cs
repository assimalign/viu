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

    // Flips once, the first time any execution flow is isolated, and never flips back. Until then
    // every lookup resolves to the shared state without consulting the execution context.
    private static volatile bool _hasIsolatedFlows;

    internal static StateExecutionState Current =>
        _hasIsolatedFlows ? IsolatedState.Value ?? SharedState : SharedState;

    internal static IDisposable Enter()
    {
        _hasIsolatedFlows = true;
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
