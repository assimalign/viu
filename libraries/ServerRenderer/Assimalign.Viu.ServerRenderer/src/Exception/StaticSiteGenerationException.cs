using System;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Identifies the first route whose mapping, composition, navigation, render, teardown, or write failed.</summary>
/// <remarks>Earlier completed documents remain available; the failing render is never published. Specified by <c>[SSG-5]</c>.</remarks>
public sealed class StaticSiteGenerationException : Exception
{
    /// <summary>Creates a failure that retains the route and original cause.</summary>
    /// <param name="route">The failing requested route.</param>
    /// <param name="innerException">The original failure.</param>
    public StaticSiteGenerationException(string route, Exception innerException)
        : base($"Static prerender failed for route '{route}': {innerException.Message}", innerException)
    {
        Route = route;
    }

    /// <summary>Gets the original route that failed, including its query and fragment.</summary>
    public string Route { get; }
}
