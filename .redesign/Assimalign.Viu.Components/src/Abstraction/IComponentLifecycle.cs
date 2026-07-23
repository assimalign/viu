using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Components;

/// <summary>
/// Registers lifecycle callbacks and exposes the cancellation token owned by one mounted template
/// instance.
/// </summary>
/// <remarks>
/// Core observes tasks returned by asynchronous callbacks and routes their faults through component
/// error handling. Ordinary lifecycle progression does not await asynchronous callbacks; only the
/// synchronous portion before their first incomplete await runs within the named lifecycle phase.
/// Server-side rendering awaits server-prefetch callbacks. Repeated callbacks, including updated
/// callbacks, may overlap unless application code prevents it. Core treats a synchronous factory
/// exception or a returned <see langword="null"/> task as a lifecycle error. A fault observed after
/// unmount bypasses disposed component hooks and goes to the application error handler or host.
/// This is Viu's typed C# counterpart to Vue's Composition API lifecycle hooks:
/// https://vuejs.org/api/composition-api-lifecycle.html.
/// </remarks>
public interface IComponentLifecycle
{
    /// <summary>
    /// Gets the token canceled after before-unmount callbacks are invoked and before the component
    /// effect scope and rendered subtree are torn down.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>Registers a synchronous callback that runs before the initial subtree is mounted.</summary>
    /// <param name="callback">The instance-local callback.</param>
    void OnBeforeMount(Action callback);

    /// <summary>Registers an observed asynchronous callback that starts before the initial subtree is mounted.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    void OnBeforeMount(Func<Task> callback);

    /// <summary>Registers an observed asynchronous callback that starts before the initial subtree is mounted.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    void OnBeforeMount(Func<CancellationToken, Task> callback);

    /// <summary>Registers a synchronous callback that runs after the initial subtree is mounted.</summary>
    /// <param name="callback">The instance-local callback.</param>
    void OnMounted(Action callback);

    /// <summary>
    /// Registers an asynchronous callback that starts after the initial subtree is mounted.
    /// </summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    void OnMounted(Func<Task> callback);

    /// <summary>
    /// Registers an asynchronous callback that starts after the initial subtree is mounted.
    /// </summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    void OnMounted(Func<CancellationToken, Task> callback);

    /// <summary>Registers a synchronous callback that runs before a later subtree is patched.</summary>
    /// <param name="callback">The instance-local callback.</param>
    void OnBeforeUpdate(Action callback);

    /// <summary>Registers an observed asynchronous callback that starts before a later subtree is patched.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    void OnBeforeUpdate(Func<Task> callback);

    /// <summary>Registers an observed asynchronous callback that starts before a later subtree is patched.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    void OnBeforeUpdate(Func<CancellationToken, Task> callback);

    /// <summary>Registers a synchronous callback that runs after a later subtree is patched.</summary>
    /// <param name="callback">The instance-local callback.</param>
    void OnUpdated(Action callback);

    /// <summary>Registers an observed asynchronous callback that starts after a later subtree is patched.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    void OnUpdated(Func<Task> callback);

    /// <summary>Registers an observed asynchronous callback that starts after a later subtree is patched.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    void OnUpdated(Func<CancellationToken, Task> callback);

    /// <summary>Registers a synchronous callback that runs before teardown starts.</summary>
    /// <param name="callback">The instance-local callback.</param>
    void OnBeforeUnmount(Action callback);

    /// <summary>Registers an observed asynchronous callback that starts before teardown.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    void OnBeforeUnmount(Func<Task> callback);

    /// <summary>Registers an observed asynchronous callback that starts before teardown.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    void OnBeforeUnmount(Func<CancellationToken, Task> callback);

    /// <summary>Registers a synchronous callback that runs after teardown completes.</summary>
    /// <param name="callback">The instance-local callback.</param>
    void OnUnmounted(Action callback);

    /// <summary>Registers an observed asynchronous callback that starts after teardown completes.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    void OnUnmounted(Func<Task> callback);

    /// <summary>Registers an observed asynchronous callback that starts after teardown completes.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory. The supplied component-lifetime token is already
    /// canceled when this callback starts.
    /// </param>
    void OnUnmounted(Func<CancellationToken, Task> callback);

    /// <summary>
    /// Registers a callback that captures an error from one of this component's descendants.
    /// </summary>
    /// <param name="callback">
    /// The callback receiving the exception, source component context when available, and diagnostic
    /// information. Returning <see langword="false"/> stops further propagation.
    /// </param>
    void OnErrorCaptured(Func<Exception, IComponentContext?, string, bool> callback);

    /// <summary>Registers a task that server-side rendering awaits before serializing the component.</summary>
    /// <param name="callback">The server-prefetch task factory.</param>
    void OnServerPrefetch(Func<Task> callback);

    /// <summary>Registers a task that server-side rendering awaits before serializing the component.</summary>
    /// <param name="callback">
    /// The server-prefetch task factory that receives the component-lifetime token.
    /// </param>
    void OnServerPrefetch(Func<CancellationToken, Task> callback);

    /// <summary>Registers a synchronous callback that runs when a cached subtree is reactivated.</summary>
    /// <param name="callback">The instance-local callback.</param>
    void OnActivated(Action callback);

    /// <summary>Registers an observed asynchronous callback that starts when a cached subtree is reactivated.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    void OnActivated(Func<Task> callback);

    /// <summary>Registers an observed asynchronous callback that starts when a cached subtree is reactivated.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    void OnActivated(Func<CancellationToken, Task> callback);

    /// <summary>Registers a synchronous callback that runs when a cached subtree is deactivated.</summary>
    /// <param name="callback">The instance-local callback.</param>
    void OnDeactivated(Action callback);

    /// <summary>Registers an observed asynchronous callback that starts when a cached subtree is deactivated.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    void OnDeactivated(Func<Task> callback);

    /// <summary>Registers an observed asynchronous callback that starts when a cached subtree is deactivated.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    void OnDeactivated(Func<CancellationToken, Task> callback);
}
