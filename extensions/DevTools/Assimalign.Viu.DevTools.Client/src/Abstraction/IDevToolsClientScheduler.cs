using System;

namespace Assimalign.Viu.DevTools.Client;

/// <summary>Defers bounded client work to a later frame on its owning event loop. Specified by <c>[DVT-14]</c>.</summary>
public interface IDevToolsClientScheduler
{
    /// <summary>Schedules one callback; implementations must never invoke it inline.</summary>
    void Schedule(Action callback);
}
