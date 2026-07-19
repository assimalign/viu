using System;
using System.Runtime.ExceptionServices;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// Routes component errors up the <c>OnErrorCaptured</c> chain — the C# port of
/// <c>handleError</c> in <c>packages/runtime-core/src/errorHandling.ts</c>. Each ancestor's
/// captured hooks receive <c>(exception, instance, info)</c>; returning false stops
/// propagation. An error no hook stopped is delivered to the app-level
/// <see cref="ApplicationConfiguration.ErrorHandler"/> ([V01.01.03.12]); with no handler set it
/// rethrows to the host (crash loudly).
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
        // Last resort: the app-level errorHandler (upstream: logError -> appContext.config
        // .errorHandler). When set it terminates propagation — an error thrown by the handler
        // itself is reported as a warning rather than re-routed, so it cannot loop.
        var errorHandler = instance?.AppContext?.Config.ErrorHandler;
        if (errorHandler is not null)
        {
            try
            {
                errorHandler(exception, instance, info);
            }
            catch (Exception handlerException)
            {
                RuntimeWarnings.Warn($"App-level errorHandler threw while handling an error: {handlerException}");
            }
            return;
        }
        // No handler configured: surface the failure with its original stack intact.
        ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
