using System;
using System.Threading;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>
/// Selects request-local Core, Reactivity, and State ambient execution state while retaining the
/// shared single-event-loop default for Browser applications.
/// </summary>
internal static class CoreExecutionIsolation
{
    private static readonly AsyncLocal<CoreExecutionState?> IsolatedState = new();
    private static readonly CoreExecutionState SharedState = new();

    // Flips once, the first time any execution flow is isolated, and never flips back. Until then
    // every lookup resolves to the shared state without consulting the execution context, which
    // keeps the Browser's single-event-loop hot paths free of AsyncLocal reads.
    private static volatile bool _hasIsolatedFlows;

    internal static CoreExecutionState Current =>
        _hasIsolatedFlows ? IsolatedState.Value ?? SharedState : SharedState;

    internal static IDisposable Enter()
    {
        _hasIsolatedFlows = true;
        CoreExecutionState? previous = IsolatedState.Value;
        IsolatedState.Value = new CoreExecutionState();
        IDisposable reactivity = Reactive.EnterExecutionFlow();
        IDisposable state = StateStores.EnterExecutionFlow();
        return new IsolationLease(previous, reactivity, state);
    }

    private sealed class IsolationLease : IDisposable
    {
        private readonly CoreExecutionState? _previous;
        private readonly IDisposable _reactivity;
        private readonly IDisposable _state;
        private bool _isDisposed;

        internal IsolationLease(
            CoreExecutionState? previous,
            IDisposable reactivity,
            IDisposable state)
        {
            _previous = previous;
            _reactivity = reactivity;
            _state = state;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            try
            {
                _state.Dispose();
            }
            finally
            {
                try
                {
                    _reactivity.Dispose();
                }
                finally
                {
                    IsolatedState.Value = _previous;
                }
            }
        }
    }
}
