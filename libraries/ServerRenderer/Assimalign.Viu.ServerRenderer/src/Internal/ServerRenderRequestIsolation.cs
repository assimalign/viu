using System;
using System.Runtime.CompilerServices;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Tracks request identities weakly across every typed server adaptor instance.</summary>
internal static class ServerRenderRequestIsolation
{
    private static readonly object UsedMarker = new();
    private static readonly ConditionalWeakTable<ServerRenderApplication, object> UsedApplications =
        new();
    private static readonly ConditionalWeakTable<SsrContext, object> UsedContexts = new();

    internal static void Track(
        ServerRenderApplication application,
        SsrContext context)
    {
        ArgumentException? applicationReuse = null;
        try
        {
            UsedApplications.Add(application, UsedMarker);
        }
        catch (ArgumentException exception)
        {
            applicationReuse = exception;
        }

        ArgumentException? contextReuse = null;
        try
        {
            UsedContexts.Add(context, UsedMarker);
        }
        catch (ArgumentException exception)
        {
            contextReuse = exception;
        }

        if (applicationReuse is null && contextReuse is null)
        {
            return;
        }

        if (applicationReuse is not null && contextReuse is not null)
        {
            throw new InvalidOperationException(
                "Server render application and SsrContext instances cannot be reused across host "
                    + "requests.",
                new AggregateException(applicationReuse, contextReuse));
        }

        throw applicationReuse is not null
            ? new InvalidOperationException(
                "A server render application instance cannot be reused across host requests.",
                applicationReuse)
            : new InvalidOperationException(
                "An SsrContext instance cannot be reused across host requests.",
                contextReuse);
    }
}
