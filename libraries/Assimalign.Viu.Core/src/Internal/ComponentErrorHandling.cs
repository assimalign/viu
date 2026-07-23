using System;
using System.Runtime.ExceptionServices;

namespace Assimalign.Viu;

/// <summary>Routes component runtime errors through ancestor capture hooks and the application.</summary>
internal static class ComponentErrorHandling
{
    internal static void Handle(
        Exception exception,
        ComponentContext? source,
        string diagnosticInformation,
        bool rethrowIfUnhandled = true)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrEmpty(diagnosticInformation);

        for (ComponentContext? ancestor = source?.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            try
            {
                if (!ancestor.Lifecycle.Capture(
                    ancestor,
                    exception,
                    source,
                    diagnosticInformation))
                {
                    return;
                }
            }
            catch (Exception captureException)
            {
                exception = captureException;
                source = ancestor;
            }
        }

        IApplicationContext? application = source?.Application;
        if (application?.ErrorHandler is not null)
        {
            application.ErrorHandler(exception, source, diagnosticInformation);
            return;
        }

        if (rethrowIfUnhandled)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    internal static void HandleObservedTaskError(
        Exception exception,
        ComponentContext source,
        string diagnosticInformation)
    {
        try
        {
            Handle(exception, source.IsUnmounted ? null : source, diagnosticInformation);
        }
        catch (Exception unhandled)
        {
            ExceptionDispatchInfo captured = ExceptionDispatchInfo.Capture(unhandled);
            Scheduler.QueuePostFlushCallback(
                new SchedulerJob(captured.Throw)
                {
                    Name = diagnosticInformation,
                });
        }
    }
}
