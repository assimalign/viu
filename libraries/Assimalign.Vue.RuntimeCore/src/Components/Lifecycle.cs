using System;
using System.Threading.Tasks;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// Composition API lifecycle registration — the C# port of
/// <c>packages/runtime-core/src/apiLifecycle.ts</c>
/// (https://vuejs.org/api/composition-api-lifecycle.html). Hooks bind to
/// <see cref="ComponentInstance.Current"/> at registration time (so they must be called during
/// <c>Setup</c>), run in registration order, and are wrapped in error handling that routes
/// through the <c>OnErrorCaptured</c> chain. Registering with no active instance produces the
/// upstream dev warning and is otherwise ignored.
/// </summary>
public static class Lifecycle
{
    /// <summary>Runs before the instance's first render is patched in (parent before child).</summary>
    /// <param name="hook">The hook.</param>
    public static void OnBeforeMount(Action hook) => Register(LifecycleHookKind.BeforeMount, hook, nameof(OnBeforeMount));

    /// <summary>Runs after the instance's tree is in the host — post-flush, child before parent.</summary>
    /// <param name="hook">The hook.</param>
    public static void OnMounted(Action hook) => Register(LifecycleHookKind.Mounted, hook, nameof(OnMounted));

    /// <summary>Runs before a re-render patches; the pre-patch subtree is still observable.</summary>
    /// <param name="hook">The hook.</param>
    public static void OnBeforeUpdate(Action hook) => Register(LifecycleHookKind.BeforeUpdate, hook, nameof(OnBeforeUpdate));

    /// <summary>Runs after a re-render's patches applied (post-flush).</summary>
    /// <param name="hook">The hook.</param>
    public static void OnUpdated(Action hook) => Register(LifecycleHookKind.Updated, hook, nameof(OnUpdated));

    /// <summary>Runs before teardown starts (parent before child).</summary>
    /// <param name="hook">The hook.</param>
    public static void OnBeforeUnmount(Action hook) => Register(LifecycleHookKind.BeforeUnmount, hook, nameof(OnBeforeUnmount));

    /// <summary>Runs after teardown (post-flush, child before parent); the last hooks an instance fires.</summary>
    /// <param name="hook">The hook.</param>
    public static void OnUnmounted(Action hook) => Register(LifecycleHookKind.Unmounted, hook, nameof(OnUnmounted));

    /// <summary>
    /// Captures descendant errors as <c>(exception, source instance, info)</c>; return false to
    /// stop propagation to ancestors and the app-level handler.
    /// </summary>
    /// <param name="hook">The capture hook.</param>
    public static void OnErrorCaptured(Func<Exception, ComponentInstance?, string, bool> hook)
        => Register(LifecycleHookKind.ErrorCaptured, hook, nameof(OnErrorCaptured));

    /// <summary>
    /// Registered for the server renderer to await before serializing ([V01.01.07.01]);
    /// a no-op in client-only rendering.
    /// </summary>
    /// <param name="hook">The prefetch task factory.</param>
    public static void OnServerPrefetch(Func<Task> hook) => Register(LifecycleHookKind.ServerPrefetch, hook, nameof(OnServerPrefetch));

    /// <summary>Stored for KeepAlive activation ([V01.01.03.18]); a no-op without a KeepAlive parent.</summary>
    /// <param name="hook">The hook.</param>
    public static void OnActivated(Action hook) => Register(LifecycleHookKind.Activated, hook, nameof(OnActivated));

    /// <summary>Stored for KeepAlive deactivation ([V01.01.03.18]); a no-op without a KeepAlive parent.</summary>
    /// <param name="hook">The hook.</param>
    public static void OnDeactivated(Action hook) => Register(LifecycleHookKind.Deactivated, hook, nameof(OnDeactivated));

    private static void Register(LifecycleHookKind kind, Delegate hook, string apiName)
    {
        ArgumentNullException.ThrowIfNull(hook);
        var instance = ComponentInstance.Current;
        if (instance is null)
        {
            RuntimeWarnings.Warn(
                $"{apiName}() is called when there is no active component instance to be associated with. "
                + "Lifecycle hooks can only be registered during Setup().");
            return;
        }
        instance.RegisterHook(kind, hook);
    }
}
