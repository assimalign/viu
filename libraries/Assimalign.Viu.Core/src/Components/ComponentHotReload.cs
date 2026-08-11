using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Assimalign.Viu;

/// <summary>
/// Provides the hidden compiler/runtime registration and update application binary interface for
/// component Hot Reload.
/// </summary>
/// <remarks>
/// Generated marker types are compared by identity; Core performs no metadata inspection or
/// reflective activation. The ambient registry follows Viu's single-threaded host event-loop
/// model and is not thread-safe. Specified by <c>[SFC-CG-2]</c> and <c>[SFC-CG-4]</c>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ComponentHotReload
{
    private static readonly Dictionary<Type, ComponentHotReloadMetadata> Metadata = [];
    private static readonly Dictionary<Type, List<ComponentHotReloadUpdateRegistration>>
        UpdateHandlers = [];

    /// <summary>Registers generated marker identities for one component type.</summary>
    /// <param name="componentType">The generated component type.</param>
    /// <param name="identifier">The stable generated source identifier.</param>
    /// <param name="templateMarker">The generated template-block marker type.</param>
    /// <param name="scriptMarker">The generated script-block marker type.</param>
    /// <param name="styleMarker">The generated style-block marker type.</param>
    /// <remarks>
    /// Generated module initializers call this method when development metadata emission is
    /// enabled. Re-registration atomically replaces the marker set for the same type, allowing a
    /// later generated delta to refresh the registration without duplicate entries.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register(
        Type componentType,
        string identifier,
        Type templateMarker,
        Type scriptMarker,
        Type styleMarker)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        ComponentHotReloadMetadata metadata = new(
            componentType,
            templateMarker,
            scriptMarker,
            styleMarker);
        Metadata[componentType] = metadata;
    }

    /// <summary>Classifies updated marker types for one registered component.</summary>
    /// <param name="componentType">The registered component type.</param>
    /// <param name="updatedTypes">
    /// The runtime-supplied updated types, or null when every registration must conservatively
    /// reset.
    /// </param>
    /// <returns>The highest-precedence registered change.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ComponentHotReloadChangeKind Classify(
        Type componentType,
        Type[]? updatedTypes)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (!Metadata.TryGetValue(componentType, out ComponentHotReloadMetadata? metadata))
        {
            return ComponentHotReloadChangeKind.None;
        }

        return metadata.Classify(CreateUpdatedTypeSet(updatedTypes));
    }

    /// <summary>Applies one runtime Hot Reload update to registered mounted components.</summary>
    /// <param name="updatedTypes">
    /// The runtime-supplied updated types, or null when every registered component must reset.
    /// </param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void ApplyUpdates(Type[]? updatedTypes)
    {
        if (Metadata.Count == 0)
        {
            return;
        }

        IReadOnlySet<Type>? updatedTypeSet = CreateUpdatedTypeSet(updatedTypes);
        ComponentHotReloadMetadata[] snapshot = [.. Metadata.Values];
        foreach (ComponentHotReloadMetadata metadata in snapshot)
        {
            ComponentHotReloadChangeKind change = metadata.Classify(updatedTypeSet);
            if (change is ComponentHotReloadChangeKind.None
                or ComponentHotReloadChangeKind.StyleOnly)
            {
                continue;
            }

            NotifyUpdateHandlers(metadata.ComponentType, change);
        }
    }

    internal static IDisposable RegisterMountedComponent(
        Type componentType,
        Action<ComponentHotReloadChangeKind> handler)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(handler);
        ComponentHotReloadUpdateRegistration registration = new(componentType, handler);
        if (!UpdateHandlers.TryGetValue(
            componentType,
            out List<ComponentHotReloadUpdateRegistration>? registrations))
        {
            registrations = [];
            UpdateHandlers.Add(componentType, registrations);
        }

        registrations.Add(registration);
        return registration;
    }

    internal static void UnregisterUpdateHandler(
        ComponentHotReloadUpdateRegistration registration)
    {
        if (!UpdateHandlers.TryGetValue(
            registration.ComponentType,
            out List<ComponentHotReloadUpdateRegistration>? registrations))
        {
            return;
        }

        registrations.Remove(registration);
        if (registrations.Count == 0)
        {
            UpdateHandlers.Remove(registration.ComponentType);
        }
    }

    private static HashSet<Type>? CreateUpdatedTypeSet(Type[]? updatedTypes)
    {
        if (updatedTypes is null)
        {
            return null;
        }

        HashSet<Type> result = [];
        foreach (Type? updatedType in updatedTypes)
        {
            if (updatedType is not null)
            {
                result.Add(updatedType);
            }
        }

        return result;
    }

    private static void NotifyUpdateHandlers(
        Type componentType,
        ComponentHotReloadChangeKind change)
    {
        if (!UpdateHandlers.TryGetValue(
            componentType,
            out List<ComponentHotReloadUpdateRegistration>? registrations))
        {
            return;
        }

        ComponentHotReloadUpdateRegistration[] snapshot = [.. registrations];
        foreach (ComponentHotReloadUpdateRegistration registration in snapshot)
        {
            registration.Invoke(change);
        }
    }
}
