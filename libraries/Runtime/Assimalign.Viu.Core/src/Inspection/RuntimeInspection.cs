using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Assimalign.Viu;

/// <summary>
/// Installs the optional process-local runtime inspection hook behind a link-time feature switch.
/// </summary>
/// <remarks>
/// The runtime is single-threaded and permits one hook per logical process. With the feature
/// switch disabled, the linker folds <see cref="IsSupported"/> to false and removes guarded hook
/// calls. Hook exceptions are swallowed so diagnostics cannot alter application behavior.
/// Specified by <c>[DVT-1]</c>.
/// </remarks>
public static class RuntimeInspection
{
    /// <summary>The runtime-host feature switch controlling inspection support.</summary>
    public const string FeatureSwitchName = "Assimalign.Viu.RuntimeInspection.IsSupported";

    private static IRuntimeInspectionHook? _hook;

    /// <summary>
    /// Gets whether the application was built with runtime inspection support. The value is false
    /// unless the runtime-host feature switch is explicitly enabled.
    /// </summary>
    [FeatureSwitchDefinition(FeatureSwitchName)]
    public static bool IsSupported =>
        AppContext.TryGetSwitch(FeatureSwitchName, out bool enabled) && enabled;

    /// <summary>
    /// Gets whether inspection support is enabled and a hook is currently installed. Renderer
    /// call sites use this single check before constructing any inspection data.
    /// </summary>
    public static bool IsEnabled => IsSupported && _hook is not null;

    /// <summary>Installs the sole runtime hook until the returned lease is disposed.</summary>
    /// <param name="hook">The non-blocking hook implementation.</param>
    /// <returns>A lease that removes this exact hook.</returns>
    /// <exception cref="InvalidOperationException">
    /// Inspection support is disabled or another hook is already installed.
    /// </exception>
    public static IDisposable Use(IRuntimeInspectionHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        if (!IsSupported)
        {
            throw new InvalidOperationException(
                $"Runtime inspection is disabled. Enable the '{FeatureSwitchName}' feature switch before application startup.");
        }

        if (_hook is not null)
        {
            throw new InvalidOperationException("A runtime inspection hook is already installed.");
        }

        _hook = hook;
        return new HookRegistration(hook);
    }

    internal static void NotifyMounted(in RuntimeInspectionComponent component)
    {
        try
        {
            _hook?.ComponentMounted(in component);
        }
        catch
        {
        }
    }

    internal static void NotifyUpdated(in RuntimeInspectionComponent component)
    {
        try
        {
            _hook?.ComponentUpdated(in component);
        }
        catch
        {
        }
    }

    internal static void NotifyUnmounted(object instance)
    {
        try
        {
            _hook?.ComponentUnmounted(instance);
        }
        catch
        {
        }
    }

    internal static void NotifyReordered(object instance, int index)
    {
        try
        {
            _hook?.ComponentReordered(instance, index);
        }
        catch
        {
        }
    }

    internal static void NotifyEvent(
        object instance,
        string name,
        IReadOnlyList<object?> arguments)
    {
        try
        {
            _hook?.ComponentEvent(instance, name, arguments);
        }
        catch
        {
        }
    }

    private sealed class HookRegistration : IDisposable
    {
        private IRuntimeInspectionHook? _installed;

        internal HookRegistration(IRuntimeInspectionHook installed) =>
            _installed = installed;

        public void Dispose()
        {
            if (_installed is null)
            {
                return;
            }

            if (!ReferenceEquals(_hook, _installed))
            {
                throw new InvalidOperationException(
                    "Runtime inspection hook leases must be disposed by their installing owner.");
            }

            _hook = null;
            _installed = null;
        }
    }
}
