using System;

namespace Assimalign.Viu;

[Flags]
internal enum SchedulerJobFlags
{
    Queued = 1,
    PreFlush = 1 << 1,
    AllowRecurse = 1 << 2,
    Disposed = 1 << 3,
}
