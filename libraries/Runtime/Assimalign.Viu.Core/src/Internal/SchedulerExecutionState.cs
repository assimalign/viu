using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>Owns scheduler queues and flush bookkeeping for one logical execution flow.</summary>
internal sealed class SchedulerExecutionState
{
    internal object Synchronization { get; } = new();

    internal List<SchedulerJob> Queue { get; } = [];

    internal List<SchedulerJob> PendingPostFlushCallbacks { get; } = [];

    internal List<Action> PendingHostCommits { get; } = [];

    internal List<SchedulerJob>? ActivePostFlushCallbacks { get; set; }

    internal List<SchedulerJob>? ExecutedInFlushChain { get; set; }

    internal Action<Action>? FlushDispatcher { get; set; }

    internal int FlushIndex { get; set; } = -1;

    internal bool IsFlushing { get; set; }

    internal bool IsFlushPending { get; set; }

    internal TaskCompletionSource? FlushCompletion { get; set; }

    internal long NextInsertionSequence { get; set; }
}
