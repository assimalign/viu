using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

internal sealed class TransitionState
{
    internal bool IsMounted;

    internal bool IsUnmounting;

    internal Dictionary<object, Action<bool>> EnterCallbacks { get; } = [];

    internal Dictionary<object, Action<bool>> LeaveCallbacks { get; } = [];

    internal Dictionary<TransitionIdentity, Action<bool>> Leaving { get; } = [];
}
