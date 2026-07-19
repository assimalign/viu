using System;
using System.Collections.Generic;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Captures the <see cref="Scheduler"/>'s scheduled flushes and runs them only when the test
/// says so, making the microtask-like flush deterministic on a plain CoreCLR test host — the
/// stand-in for "let the JS event loop run" around Vue's <c>nextTick</c> flush
/// (https://vuejs.org/api/general.html#nexttick). Installed via the scheduler's internal
/// dispatcher seam rather than an ambient <see cref="System.Threading.SynchronizationContext"/>,
/// so test-framework thread hops cannot strand a flush. Install in a <c>using</c> block;
/// disposal restores the previous dispatcher. Not thread-safe by design.
/// </summary>
public sealed class TestSchedulerPump : IDisposable
{
    private readonly Queue<Action> _pendingFlushes = [];
    private readonly Action<Action>? _previousDispatcher;
    private bool _isDisposed;

    private TestSchedulerPump()
    {
        _previousDispatcher = Scheduler.FlushDispatcher;
    }

    /// <summary>The number of captured flushes waiting to run.</summary>
    public int PendingFlushCount => _pendingFlushes.Count;

    /// <summary>Creates a pump and installs it as the scheduler's flush dispatcher.</summary>
    public static TestSchedulerPump Install()
    {
        var pump = new TestSchedulerPump();
        Scheduler.FlushDispatcher = pump._pendingFlushes.Enqueue;
        return pump;
    }

    /// <summary>
    /// Runs captured flushes — including ones captured while draining — until none remain.
    /// </summary>
    /// <returns>The number of flushes run.</returns>
    public int RunUntilIdle()
    {
        var executed = 0;
        while (_pendingFlushes.Count > 0)
        {
            var flush = _pendingFlushes.Dequeue();
            flush();
            executed++;
        }
        return executed;
    }

    /// <summary>Restores the previously installed dispatcher.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        Scheduler.FlushDispatcher = _previousDispatcher;
    }
}
