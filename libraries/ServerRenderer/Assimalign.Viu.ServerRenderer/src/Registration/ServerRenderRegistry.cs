using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Stores the server-render registrations supplied by generated assembly catalogs.
/// </summary>
/// <remarks>
/// Registration and lookup are ordinal through <see cref="ComponentReference"/> value equality.
/// The registry follows Viu's single-event-loop model and is not thread-safe. A server host creates
/// and populates one explicitly; the runtime never scans an assembly. Specified by
/// <c>[SSR-TARGET-2]</c> and <c>[SSR-TARGET-3]</c>.
/// </remarks>
public sealed class ServerRenderRegistry : IServerRenderRegistry
{
    private readonly Dictionary<ComponentReference, ServerRenderRegistration> _registrations = [];

    /// <summary>Registers one compiler-produced server render, throwing on a duplicate identity.</summary>
    /// <param name="registration">The immutable generated registration.</param>
    public void Register(ServerRenderRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _registrations.Add(registration.Reference, registration);
    }

    /// <inheritdoc/>
    public bool TryResolve(
        ComponentReference reference,
        out ServerRenderRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return _registrations.TryGetValue(reference, out registration);
    }
}
