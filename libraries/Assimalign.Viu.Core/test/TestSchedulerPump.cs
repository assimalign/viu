using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Tests;

/// <summary>Provides deterministic control over the runtime scheduler in unit tests.</summary>
internal sealed class TestSchedulerPump : IDisposable
{
    private readonly Queue<Action> _pendingFlushes = [];
    private readonly Action<Action>? _previousDispatcher;
    private bool _isDisposed;

    private TestSchedulerPump()
    {
        _previousDispatcher = Scheduler.FlushDispatcher;
    }

    internal int PendingFlushCount => _pendingFlushes.Count;

    internal static TestSchedulerPump Install()
    {
        TestSchedulerPump pump = new();
        Scheduler.FlushDispatcher = pump._pendingFlushes.Enqueue;
        return pump;
    }

    internal int RunUntilIdle()
    {
        int executed = 0;
        while (_pendingFlushes.Count > 0)
        {
            Action flush = _pendingFlushes.Dequeue();
            flush();
            executed++;
        }

        return executed;
    }

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
