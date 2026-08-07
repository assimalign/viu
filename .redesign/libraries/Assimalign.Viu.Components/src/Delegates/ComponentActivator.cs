using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Produces one authored component instance per mount through direct delegate dispatch — the
/// only activation path; there is no reflection-based construction.
/// </summary>
/// <remarks>Specified by <c>[CMP-4]</c> and <c>[CMP-11]</c>.</remarks>
/// <param name="services">The application-composed provider, or null when none is configured.</param>
/// <returns>The authored instance.</returns>
public delegate IComponent ComponentActivator(IServiceProvider? services);
