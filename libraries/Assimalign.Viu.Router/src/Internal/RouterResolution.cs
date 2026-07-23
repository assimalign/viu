using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// Resolves the active <see cref="Router"/> from the application-owned service provider exposed by
/// one component context. Router deliberately does not prescribe how that provider is composed.
/// </summary>
internal static class RouterResolution
{
    /// <summary>
    /// Returns the router for <paramref name="context"/>, or null when the application provider does
    /// not expose one.
    /// </summary>
    /// <param name="context">The explicit mounted component context.</param>
    /// <returns>The active router, or null.</returns>
    internal static Router? Resolve(IComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Services.GetService(typeof(Router)) as Router;
    }
}
