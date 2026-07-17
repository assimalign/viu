using System;
using System.Runtime.ExceptionServices;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// Routes component errors up the <c>OnErrorCaptured</c> chain — the C# port of
/// <c>handleError</c> in <c>packages/runtime-core/src/errorHandling.ts</c>. Each ancestor's
/// captured hooks receive <c>(exception, instance, info)</c>; returning false stops
/// propagation. An unhandled error rethrows to the host until the app-level handler lands
/// ([V01.01.03.12]).
/// </summary>
internal static class ComponentErrorHandling
{
    internal static void Handle(Exception exception, ComponentInstance? instance, string info)
    {
        for (var ancestor = instance?.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            var hooks = ancestor.GetHooks(LifecycleHookKind.ErrorCaptured);
            if (hooks is null)
            {
                continue;
            }
            foreach (var hook in hooks)
            {
                if (hook is Func<Exception, ComponentInstance?, string, bool> capture
                    && !capture(exception, instance, info))
                {
                    return; // false stops propagation (upstream parity)
                }
            }
        }
        // No app-level handler yet ([V01.01.03.12]); surface the failure with its original
        // stack intact.
        ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
