using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

internal sealed class TransitionState
{
    internal bool IsMounted;

    internal bool IsLeaving;

    internal bool IsUnmounting;

    internal Dictionary<object, Action<bool>> EnterCallbacks { get; } = new();

    internal Dictionary<object, Action<bool>> LeaveCallbacks { get; } = new();

    internal Dictionary<TransitionIdentity, TransitionHooks> Leaving { get; } = new();
}
