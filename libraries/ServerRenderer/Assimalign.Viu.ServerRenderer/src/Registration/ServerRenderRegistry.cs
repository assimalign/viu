using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Stores the server-render registrations supplied by generated assembly catalogs.
/// </summary>
/// <remarks>
/// Registration and lookup are ordinal through <see cref="ComponentReference"/> value equality.
/// A server host creates and populates one explicitly, then freezes it before sharing it across
/// concurrent requests. Registration and lookup are synchronized before that transition; frozen
/// lookup uses an immutable snapshot without locking. The runtime never scans an assembly.
/// Specified by <c>[SSR-TARGET-2]</c>, <c>[SSR-TARGET-3]</c>, and <c>[SSR-TARGET-4]</c>.
/// </remarks>
public sealed class ServerRenderRegistry : IServerRenderRegistry
{
    private readonly object _synchronization = new();
    private Dictionary<ComponentReference, ServerRenderRegistration>? _registrations = [];
    private FrozenDictionary<ComponentReference, ServerRenderRegistration>? _frozenRegistrations;

    /// <summary>
    /// Registers one compiler-produced server render, throwing when frozen or on a duplicate
    /// identity.
    /// </summary>
    /// <param name="registration">The immutable generated registration.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="registration"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">The registry has already been frozen.</exception>
    public void Register(ServerRenderRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (_synchronization)
        {
            if (_registrations is null)
            {
                throw new InvalidOperationException(
                    "The server render registry is frozen and cannot accept registrations.");
            }

            _registrations.Add(registration.Reference, registration);
        }
    }

    /// <summary>
    /// Freezes registration and exposes this registry through its resolution-only contract.
    /// </summary>
    /// <returns>This registry after its immutable lookup snapshot has been safely published.</returns>
    /// <remarks>
    /// The transition is idempotent. After it returns, registration is rejected and concurrent
    /// resolution is lock-free. Specified by <c>[SSR-TARGET-4]</c>.
    /// </remarks>
    public IServerRenderRegistry Freeze()
    {
        if (Volatile.Read(ref _frozenRegistrations) is not null)
        {
            return this;
        }

        lock (_synchronization)
        {
            if (_frozenRegistrations is null)
            {
                FrozenDictionary<ComponentReference, ServerRenderRegistration> frozenRegistrations =
                    _registrations!.ToFrozenDictionary();
                _registrations = null;
                Volatile.Write(ref _frozenRegistrations, frozenRegistrations);
            }
        }

        return this;
    }

    /// <inheritdoc/>
    public bool TryResolve(
        ComponentReference reference,
        out ServerRenderRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(reference);
        FrozenDictionary<ComponentReference, ServerRenderRegistration>? frozenRegistrations =
            Volatile.Read(ref _frozenRegistrations);
        if (frozenRegistrations is not null)
        {
            return frozenRegistrations.TryGetValue(reference, out registration);
        }

        lock (_synchronization)
        {
            frozenRegistrations = _frozenRegistrations;
            return frozenRegistrations is not null
                ? frozenRegistrations.TryGetValue(reference, out registration)
                : _registrations!.TryGetValue(reference, out registration);
        }
    }
}
