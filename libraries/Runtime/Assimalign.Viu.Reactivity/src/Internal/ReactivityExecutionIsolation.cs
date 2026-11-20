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

    // Flips once, the first time any execution flow is isolated, and never flips back. Until then
    // every reactive read and trigger resolves the shared state without consulting the execution
    // context, which keeps the Browser's single-event-loop hot paths free of AsyncLocal reads.
    private static volatile bool _hasIsolatedFlows;

    internal static ReactivityExecutionState Current =>
        _hasIsolatedFlows ? IsolatedState.Value ?? SharedState : SharedState;

    internal static IDisposable Enter()
    {
        _hasIsolatedFlows = true;
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
