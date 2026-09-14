using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>
/// Installs the optional process-local runtime inspection hook behind a link-time feature switch.
/// </summary>
/// <remarks>
/// The runtime is single-threaded and permits one hook per logical process. With the feature
/// switch disabled, the linker folds <see cref="IsSupported"/> to false and removes guarded hook
/// calls. Hook exceptions are swallowed so diagnostics cannot alter application behavior.
/// Specified by <c>[DVT-1]</c>, <c>[DVT-8]</c>, and <c>[DVT-12]</c>.
/// </remarks>
public static class RuntimeInspection
{
    /// <summary>The runtime-host feature switch controlling inspection support.</summary>
    public const string FeatureSwitchName = "Assimalign.Viu.RuntimeInspection.IsSupported";

    // Read once at type initialization so the per-job scheduler guard is one field read; the
    // trimmer substitutes the configured constant. Specified by [DVT-12].
    private static readonly bool _isSupported =
        AppContext.TryGetSwitch(FeatureSwitchName, out bool enabled) && enabled;
    private static IRuntimeInspectionHook? _hook;
    private static IRuntimeSchedulerInspectionHook? _schedulerHook;

    /// <summary>
    /// Gets whether the application was built with runtime inspection support. The value is false
    /// unless the runtime-host feature switch is explicitly enabled; it is read once when this type
    /// initializes, so later changes have no effect. Specified by <c>[DVT-12]</c>.
    /// </summary>
    [FeatureSwitchDefinition(FeatureSwitchName)]
    public static bool IsSupported => _isSupported;

    /// <summary>
    /// Gets whether inspection support is enabled and a hook is currently installed. Renderer
    /// call sites use this single check before constructing any inspection data.
    /// </summary>
    public static bool IsEnabled => IsSupported && _hook is not null;

    /// <summary>
    /// Gets the current execution flow's current or next flush identifier, or zero when inspection
    /// is disabled. Reading reserves a stable identifier without scheduling work, so writes before
    /// a flush is queued can be correlated with that flush. Specified by <c>[DVT-10]</c>.
    /// </summary>
    public static long FlushIdentifier => IsEnabled ? Scheduler.GetInspectionFlushIdentifier() : 0;

    /// <summary>
    /// Installs the sole runtime hook until the returned lease is disposed, including scheduler
    /// observations when the hook also implements <see cref="IRuntimeSchedulerInspectionHook"/>.
    /// Specified by <c>[DVT-8]</c>.
    /// </summary>
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
        _schedulerHook = hook as IRuntimeSchedulerInspectionHook;
        return new HookRegistration(hook);
    }

    internal static void NotifyFlushStarted(in RuntimeInspectionFlush flush)
    {
        Reactive.PauseTracking();
        try
        {
            _schedulerHook?.FlushStarted(in flush);
        }
        catch
        {
        }
        finally
        {
            Reactive.ResetTracking();
        }
    }

    internal static void NotifyFlushCompleted(in RuntimeInspectionFlush flush)
    {
        Reactive.PauseTracking();
        try
        {
            _schedulerHook?.FlushCompleted(in flush);
        }
        catch
        {
        }
        finally
        {
            Reactive.ResetTracking();
        }
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
            _schedulerHook = null;
            _installed = null;
        }
    }
}
