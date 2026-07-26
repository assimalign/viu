using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Applies compiler-generated component metadata updates to mounted development-time components.
/// </summary>
/// <remarks>
/// A generated assembly metadata-update handler forwards its updated type set to
/// <see cref="ApplyUpdates"/> after the runtime applies a Hot Reload delta. Compiler-generated marker
/// types identify the changed block without reflection. Style-only changes do no component work
/// because the Viu CSS worker owns stylesheet replacement. Managed template and script changes tear
/// down each affected component ownership unit and remount it at the same host position so .NET 10
/// browser WebAssembly executes the updated generated code instead of an already transformed stale
/// call site. <see cref="ScriptUpdateRequiresReset"/> gives the host advance notice of script resets
/// so it can supersede that component-local reset with a full application reload.
///
/// This ambient registry follows Viu's single-threaded browser event-loop model and is not
/// thread-safe.
///
/// This is the runtime half of [V01.01.06.05]:
/// https://github.com/assimalign/viu/issues/61.
/// </remarks>
public static class ComponentHotReload
{
    private static readonly Dictionary<Type, List<ComponentHotReloadRegistration>>
        _registrations = [];

    /// <summary>
    /// Occurs once per updated component type immediately before Core resets its mounted instances
    /// for a script-block change.
    /// </summary>
    /// <remarks>
    /// Core deliberately tears down the previous component scope instead of rerunning setup against
    /// existing state, which would retain effects and lifecycle resources created by the previous
    /// script. A browser host may handle this event to perform a full application reload instead.
    /// </remarks>
    public static event ComponentScriptUpdateResetHandler? ScriptUpdateRequiresReset;

    /// <summary>
    /// Applies updated component method bodies to the corresponding mounted component instances.
    /// </summary>
    /// <param name="updatedTypes">
    /// The types supplied by <c>MetadataUpdateHandler.UpdateApplication</c>, or null when the
    /// runtime does not identify the changed types and every registered component must be checked.
    /// </param>
    public static void ApplyUpdates(Type[]? updatedTypes)
    {
        if (_registrations.Count == 0)
        {
            return;
        }

        HashSet<Type>? updatedTypeSet = null;
        if (updatedTypes is not null)
        {
            updatedTypeSet = [];
            for (int index = 0; index < updatedTypes.Length; index++)
            {
                Type? updatedType = updatedTypes[index];
                if (updatedType is not null)
                {
                    updatedTypeSet.Add(updatedType);
                }
            }
        }

        Type[] registeredTypes = new Type[_registrations.Count];
        _registrations.Keys.CopyTo(registeredTypes, 0);
        for (int index = 0; index < registeredTypes.Length; index++)
        {
            ApplyUpdate(registeredTypes[index], updatedTypeSet);
        }
    }

    internal static IDisposable Register(
        Type componentType,
        IComponentHotReloadMetadata metadata,
        Action resetComponentUpdate)
    {
        ComponentHotReloadRegistration registration = new(
            componentType,
            metadata,
            resetComponentUpdate);
        if (!_registrations.TryGetValue(
            componentType,
            out List<ComponentHotReloadRegistration>? registrations))
        {
            registrations = [];
            _registrations.Add(componentType, registrations);
        }

        registrations.Add(registration);
        return registration;
    }

    internal static void Unregister(ComponentHotReloadRegistration registration)
    {
        if (!_registrations.TryGetValue(
            registration.ComponentType,
            out List<ComponentHotReloadRegistration>? registrations))
        {
            return;
        }

        registrations.Remove(registration);
        if (registrations.Count == 0)
        {
            _registrations.Remove(registration.ComponentType);
        }
    }

    internal static void Reset()
    {
        _registrations.Clear();
        ScriptUpdateRequiresReset = null;
    }

    private static void ApplyUpdate(
        Type componentType,
        IReadOnlySet<Type>? updatedTypes)
    {
        if (!_registrations.TryGetValue(
            componentType,
            out List<ComponentHotReloadRegistration>? registrations))
        {
            return;
        }

        ComponentHotReloadRegistration[] snapshot = registrations.ToArray();
        bool templateUpdateRequiresReset = false;
        string? componentIdentifier = null;
        bool scriptUpdateRequiresReset = false;

        for (int index = 0; index < snapshot.Length; index++)
        {
            ComponentHotReloadRegistration registration = snapshot[index];
            ComponentHotReloadChangeKind change = registration.Classify(updatedTypes);
            componentIdentifier ??= registration.ComponentIdentifier;
            if (change == ComponentHotReloadChangeKind.ScriptReset)
            {
                scriptUpdateRequiresReset = true;
            }
            else if (change == ComponentHotReloadChangeKind.Template)
            {
                templateUpdateRequiresReset = true;
            }
        }

        if (scriptUpdateRequiresReset)
        {
            ScriptUpdateRequiresReset?.Invoke(
                componentIdentifier!,
                componentType);
            for (int index = 0; index < snapshot.Length; index++)
            {
                snapshot[index].ResetComponentUpdate();
            }

            return;
        }

        if (templateUpdateRequiresReset)
        {
            for (int index = 0; index < snapshot.Length; index++)
            {
                snapshot[index].ResetComponentUpdate();
            }
        }
    }
}
