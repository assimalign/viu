using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>Owns one Browser observer or event-listener registration for deferred hydration.</summary>
/// <remarks>
/// The registration is confined to the single-threaded Browser event loop. Completion replays a
/// captured interaction only after Core has activated the boundary; disposal drops it and releases
/// every JavaScript observer or listener. Specified by <c>[HYD-LAZY-3]</c> through
/// <c>[HYD-LAZY-5]</c>.
/// </remarks>
[SupportedOSPlatform("browser")]
internal sealed class BrowserLazyHydrationTriggerRegistration :
    IHydrationTriggerRegistration
{
    private int _token;

    private BrowserLazyHydrationTriggerRegistration(
        HydrationTriggerRequest<int> request)
    {
        HydrationStrategy strategy = request.Strategy;
        string? condition = strategy.Kind switch
        {
            HydrationStrategyKind.Visible => strategy.VisibilityRootMargin,
            HydrationStrategyKind.MediaQuery => strategy.MediaQuery,
            _ => null,
        };
        IReadOnlyList<string> interactionEvents = strategy.InteractionEvents;
        string[] eventNames = new string[interactionEvents.Count];
        for (int index = 0; index < interactionEvents.Count; index++)
        {
            eventNames[index] = interactionEvents[index];
        }

        _token = BrowserDomBridge.ScheduleHydrationTrigger(
            request.StartAnchor,
            request.EndAnchor,
            (int)strategy.Kind,
            strategy.IdleTimeoutMilliseconds ?? -1,
            condition,
            eventNames,
            request.Trigger);
    }

    /// <summary>Registers the host trigger represented by <paramref name="request"/>.</summary>
    /// <param name="request">The marker-bounded Core trigger request.</param>
    /// <returns>The owned trigger registration.</returns>
    internal static IHydrationTriggerRegistration Schedule(
        HydrationTriggerRequest<int> request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new BrowserLazyHydrationTriggerRegistration(request);
    }

    /// <inheritdoc />
    public void Complete()
    {
        int token = TakeToken();
        if (token != 0)
        {
            BrowserDomBridge.CompleteHydrationTrigger(token);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        int token = TakeToken();
        if (token != 0)
        {
            BrowserDomBridge.CancelHydrationTrigger(token);
        }
    }

    private int TakeToken()
    {
        int token = _token;
        _token = 0;
        return token;
    }
}
