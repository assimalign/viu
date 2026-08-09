using System;
using System.Threading;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Selects execution-flow-local bookkeeping for multi-request hosts while preserving the shared
/// single-event-loop state used by ordinary Browser applications.
/// </summary>
internal static class ReactivityExecutionIsolation
{
    private static readonly AsyncLocal<ReactivityExecutionState?> IsolatedState = new();
    private static readonly ReactivityExecutionState SharedState = new();

    internal static ReactivityExecutionState Current => IsolatedState.Value ?? SharedState;

    internal static IDisposable Enter()
    {
        ReactivityExecutionState? previous = IsolatedState.Value;
        IsolatedState.Value = new ReactivityExecutionState();
        return new IsolationLease(previous);
    }

    private sealed class IsolationLease : IDisposable
    {
        private readonly ReactivityExecutionState? _previous;
        private bool _isDisposed;

        internal IsolationLease(ReactivityExecutionState? previous)
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
