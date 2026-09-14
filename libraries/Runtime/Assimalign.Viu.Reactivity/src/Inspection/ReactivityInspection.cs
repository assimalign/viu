using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Installs one process-local reactivity observer behind the runtime inspection feature switch.
/// The seam is single-threaded and must be installed and disposed on the application's event
/// loop. Disabled calls are linker-removable and allocate nothing. Specified by <c>[DVT-8]</c>,
/// <c>[DVT-12]</c>, and <c>[RCT-13]</c>.
/// </summary>
public static class ReactivityInspection
{
    /// <summary>The shared runtime-host switch; Reactivity has no Core dependency. Specified by <c>[DVT-12]</c>.</summary>
    public const string FeatureSwitchName = "Assimalign.Viu.RuntimeInspection.IsSupported";

    // Read once at type initialization: the hot-path guard costs one field read, never a
    // switch-table lookup per tracked read. Specified by [DVT-12].
    private static readonly bool _isSupported =
        AppContext.TryGetSwitch(FeatureSwitchName, out bool enabled) && enabled;
    private static IReactivityInspectionHook? _hook;
    private static bool _isObserving;
    private static ConditionalWeakTable<Dependency, ReactivityInspectionAttribution>? _attributions;

    /// <summary>
    /// Whether the host explicitly enabled link-time inspection support. The switch is read once
    /// when this type initializes; later changes have no effect, and the trimmer substitutes the
    /// configured constant. Specified by <c>[DVT-12]</c>.
    /// </summary>
    [FeatureSwitchDefinition(FeatureSwitchName)]
    public static bool IsSupported => _isSupported;

    /// <summary>The single hot-path guard, checked before creating any inspection payload. Specified by <c>[DVT-12]</c>.</summary>
    public static bool IsEnabled => IsSupported && _hook is not null;

    /// <summary>Installs the sole hook until its idempotent lease is disposed. Specified by <c>[DVT-8]</c>.</summary>
    /// <param name="hook">A passive, non-blocking observer.</param>
    /// <returns>The lease owning exactly this installation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Support is disabled or a hook is already installed.</exception>
    public static IDisposable Use(IReactivityInspectionHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        if (!IsSupported || _hook is not null)
        {
            throw new InvalidOperationException("Reactivity inspection requires enabled support and no installed hook.");
        }

        _hook = hook;
        return new ReactivityInspectionRegistration(hook);
    }

    internal static void NotifyDependency(Dependency dependency, Subscriber? subscriber,
        object? owner, object? memberKey, bool triggered, bool isWrite)
    {
        if (_isObserving)
        {
            return;
        }

        bool previousShouldTrack = ReactivityState.ShouldTrack;
        _isObserving = true;
        ReactivityState.ShouldTrack = false;
        try
        {
            string? debugLabel = (owner as ReactiveValue)?.DebugLabel;
            if (owner is not null)
            {
                _attributions ??= new();
                ReactivityInspectionAttribution attribution = _attributions.GetValue(
                    dependency, static _ => new ReactivityInspectionAttribution());
                attribution.Set(owner, memberKey as string);
            }
            else if (_attributions is not null && _attributions.TryGetValue(dependency, out var attribution))
            {
                attribution.Owner?.TryGetTarget(out owner);
                memberKey = attribution.MemberName;
                debugLabel = (owner as ReactiveValue)?.DebugLabel;
            }

            var observation = new ReactivityInspectionDependency(
                dependency, subscriber, owner, memberKey, debugLabel, isWrite);
            if (triggered)
            {
                _hook?.DependencyTriggered(in observation);
            }
            else
            {
                _hook?.DependencyTracked(in observation);
            }
        }
        catch
        {
            // Diagnostics must never change dependency execution or application exceptions.
        }
        finally
        {
            ReactivityState.ShouldTrack = previousShouldTrack;
            _isObserving = false;
        }
    }

    internal static void AttributeReference(ReactiveValue reference)
    {
        _attributions ??= new();
        _attributions.GetValue(reference.Dependency,
            static _ => new ReactivityInspectionAttribution()).Set(reference, null);
    }

    internal static void NotifyScheduled(Dependency dependency, Subscriber subscriber)
    {
        if (subscriber is not ReactiveEffect effect ||
            (subscriber.Flags & SubscriberFlags.Notified) != 0 ||
            ((subscriber.Flags & SubscriberFlags.Running) != 0 &&
             (subscriber.Flags & SubscriberFlags.AllowRecurse) == 0))
        {
            return;
        }

        NotifyEffect(effect, dependency, completed: false, succeeded: false);
    }

    internal static void NotifyEffect(ReactiveEffect effect, Dependency? cause, bool completed, bool succeeded)
    {
        if (_isObserving)
        {
            return;
        }

        bool previousShouldTrack = ReactivityState.ShouldTrack;
        _isObserving = true;
        ReactivityState.ShouldTrack = false;
        try
        {
            var observation = new ReactivityInspectionEffect(effect, cause, succeeded);
            if (cause is not null)
            {
                _hook?.EffectScheduled(in observation);
            }
            else if (completed)
            {
                _hook?.EffectRunCompleted(in observation);
            }
            else
            {
                _hook?.EffectRunStarted(in observation);
            }
        }
        catch
        {
        }
        finally
        {
            ReactivityState.ShouldTrack = previousShouldTrack;
            _isObserving = false;
        }
    }

    internal static void Remove(IReactivityInspectionHook hook)
    {
        if (ReferenceEquals(_hook, hook))
        {
            _hook = null;
        }
    }
}
