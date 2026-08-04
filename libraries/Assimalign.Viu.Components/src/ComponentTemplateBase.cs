using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Components;

/// <summary>
/// The authoring base class of a compiled single-file component: it holds the mounted
/// <see cref="IComponentContext"/> and surfaces every <see cref="IComponentLifecycle"/> registration
/// hook as a protected member, so a component registers a callback by writing
/// <c>OnMounted(...)</c> at the root of its class instead of <c>Context.Lifecycle.OnMounted(...)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The single-file-component source generator emits this type as the base of every <c>.viu</c> /
/// <c>.vue</c> partial that has a template block, and the generated setup bridge assigns
/// <see cref="Context"/> once per mount before the developer-authored <c>OnSetup</c> runs
/// (<c>[SFC-CG-4]</c>). A hand-authored <see cref="IComponentTemplate"/> may derive from it as well;
/// it then assigns <see cref="Context"/> at the head of its own <see cref="IComponentTemplate.Setup"/>
/// and gets the same root-level registration surface.
/// </para>
/// <para>
/// This type deliberately does <b>not</b> implement <see cref="IComponentTemplate"/>. The generated
/// partial implements that interface explicitly — which is what keeps the generated declaration
/// members from colliding with authored ones <c>[CMP-31]</c> — and an explicit implementation is only
/// legal on a type that lists the interface itself.
/// </para>
/// <para>
/// Every member here is a pass-through: it registers exactly what the <see cref="IComponentContext"/>
/// form registers, in the order the registrations are made, so the two forms are interchangeable and
/// may be mixed freely within one component (<c>[CMP-32]</c>). The registration surface is cold path —
/// it runs once per mount during setup — so the wrappers add no per-render or per-flush cost.
/// </para>
/// <para>
/// A derived component that declares its own member with one of these names and an identical
/// signature <b>hides</b> the inherited one under ordinary C# member-hiding rules: the authored member
/// wins at every call site inside the component, no lifecycle registration happens through it, and the
/// hidden hook stays reachable through the <see cref="Context"/> form (<c>[CMP-32]</c>). A member with
/// a different signature is an ordinary overload and hides nothing.
/// </para>
/// <para>
/// Instances are owned by one mounted component and are not thread-safe; Viu's execution model is
/// single-threaded (<c>[EXE-1]</c>).
/// </para>
/// </remarks>
public abstract class ComponentTemplateBase
{
    /// <summary>
    /// Gets or sets the mounted component's context — its arguments, slots, attributes, resolvers,
    /// lifecycle registrar, and event emitter.
    /// </summary>
    /// <value>
    /// The context assigned by the generated setup bridge, or by a hand-authored
    /// <see cref="IComponentTemplate.Setup"/>, once per mount before any lifecycle registration.
    /// Reading it before that assignment, or replacing it afterwards, is unsupported.
    /// </value>
    protected IComponentContext Context { get; set; } = null!;

    /// <summary>Registers a synchronous callback that runs before the initial subtree is mounted.</summary>
    /// <param name="callback">The instance-local callback.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnBeforeMount(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing is specified by <c>[CMP-20]</c>.
    /// </remarks>
    protected void OnBeforeMount(Action callback) => Context.Lifecycle.OnBeforeMount(callback);

    /// <summary>Registers an observed asynchronous callback that starts before the initial subtree is mounted.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnBeforeMount(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing and the observation of the returned task are specified by
    /// <c>[CMP-20]</c> and <c>[CMP-21]</c>.
    /// </remarks>
    protected void OnBeforeMount(Func<Task> callback) => Context.Lifecycle.OnBeforeMount(callback);

    /// <summary>Registers an observed asynchronous callback that starts before the initial subtree is mounted.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnBeforeMount(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing, the observation of the returned task, and the supplied
    /// token are specified by <c>[CMP-20]</c>, <c>[CMP-21]</c>, and <c>[CMP-22]</c>.
    /// </remarks>
    protected void OnBeforeMount(Func<CancellationToken, Task> callback) =>
        Context.Lifecycle.OnBeforeMount(callback);

    /// <summary>Registers a synchronous callback that runs after the initial subtree is mounted.</summary>
    /// <param name="callback">The instance-local callback.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnMounted(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing is specified by <c>[CMP-20]</c>.
    /// </remarks>
    protected void OnMounted(Action callback) => Context.Lifecycle.OnMounted(callback);

    /// <summary>Registers an asynchronous callback that starts after the initial subtree is mounted.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnMounted(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing and the observation of the returned task are specified by
    /// <c>[CMP-20]</c> and <c>[CMP-21]</c>.
    /// </remarks>
    protected void OnMounted(Func<Task> callback) => Context.Lifecycle.OnMounted(callback);

    /// <summary>Registers an asynchronous callback that starts after the initial subtree is mounted.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnMounted(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing, the observation of the returned task, and the supplied
    /// token are specified by <c>[CMP-20]</c>, <c>[CMP-21]</c>, and <c>[CMP-22]</c>.
    /// </remarks>
    protected void OnMounted(Func<CancellationToken, Task> callback) =>
        Context.Lifecycle.OnMounted(callback);

    /// <summary>Registers a synchronous callback that runs before a later subtree is patched.</summary>
    /// <param name="callback">The instance-local callback.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnBeforeUpdate(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing is specified by <c>[CMP-20]</c>.
    /// </remarks>
    protected void OnBeforeUpdate(Action callback) => Context.Lifecycle.OnBeforeUpdate(callback);

    /// <summary>Registers an observed asynchronous callback that starts before a later subtree is patched.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnBeforeUpdate(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing and the observation of the returned task are specified by
    /// <c>[CMP-20]</c> and <c>[CMP-21]</c>.
    /// </remarks>
    protected void OnBeforeUpdate(Func<Task> callback) => Context.Lifecycle.OnBeforeUpdate(callback);

    /// <summary>Registers an observed asynchronous callback that starts before a later subtree is patched.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnBeforeUpdate(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing, the observation of the returned task, and the supplied
    /// token are specified by <c>[CMP-20]</c>, <c>[CMP-21]</c>, and <c>[CMP-22]</c>.
    /// </remarks>
    protected void OnBeforeUpdate(Func<CancellationToken, Task> callback) =>
        Context.Lifecycle.OnBeforeUpdate(callback);

    /// <summary>Registers a synchronous callback that runs after a later subtree is patched.</summary>
    /// <param name="callback">The instance-local callback.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnUpdated(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing is specified by <c>[CMP-20]</c>.
    /// </remarks>
    protected void OnUpdated(Action callback) => Context.Lifecycle.OnUpdated(callback);

    /// <summary>Registers an observed asynchronous callback that starts after a later subtree is patched.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnUpdated(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing and the observation of the returned task are specified by
    /// <c>[CMP-20]</c> and <c>[CMP-21]</c>.
    /// </remarks>
    protected void OnUpdated(Func<Task> callback) => Context.Lifecycle.OnUpdated(callback);

    /// <summary>Registers an observed asynchronous callback that starts after a later subtree is patched.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnUpdated(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing, the observation of the returned task, and the supplied
    /// token are specified by <c>[CMP-20]</c>, <c>[CMP-21]</c>, and <c>[CMP-22]</c>.
    /// </remarks>
    protected void OnUpdated(Func<CancellationToken, Task> callback) =>
        Context.Lifecycle.OnUpdated(callback);

    /// <summary>Registers a synchronous callback that runs before teardown starts.</summary>
    /// <param name="callback">The instance-local callback.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnBeforeUnmount(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing relative to component cancellation is specified by
    /// <c>[CMP-20]</c> and <c>[CMP-22]</c>.
    /// </remarks>
    protected void OnBeforeUnmount(Action callback) => Context.Lifecycle.OnBeforeUnmount(callback);

    /// <summary>Registers an observed asynchronous callback that starts before teardown.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnBeforeUnmount(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing and the observation of the returned task are specified by
    /// <c>[CMP-20]</c>, <c>[CMP-21]</c>, and <c>[CMP-22]</c>.
    /// </remarks>
    protected void OnBeforeUnmount(Func<Task> callback) => Context.Lifecycle.OnBeforeUnmount(callback);

    /// <summary>Registers an observed asynchronous callback that starts before teardown.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnBeforeUnmount(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing, the observation of the returned task, and the supplied
    /// token are specified by <c>[CMP-20]</c>, <c>[CMP-21]</c>, and <c>[CMP-22]</c>.
    /// </remarks>
    protected void OnBeforeUnmount(Func<CancellationToken, Task> callback) =>
        Context.Lifecycle.OnBeforeUnmount(callback);

    /// <summary>Registers a synchronous callback that runs after teardown completes.</summary>
    /// <param name="callback">The instance-local callback.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnUnmounted(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing is specified by <c>[CMP-20]</c>.
    /// </remarks>
    protected void OnUnmounted(Action callback) => Context.Lifecycle.OnUnmounted(callback);

    /// <summary>Registers an observed asynchronous callback that starts after teardown completes.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnUnmounted(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing and the observation of the returned task are specified by
    /// <c>[CMP-20]</c> and <c>[CMP-21]</c>.
    /// </remarks>
    protected void OnUnmounted(Func<Task> callback) => Context.Lifecycle.OnUnmounted(callback);

    /// <summary>Registers an observed asynchronous callback that starts after teardown completes.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory. The supplied component-lifetime token is already
    /// canceled when this callback starts.
    /// </param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnUnmounted(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing, the observation of the returned task, and the state of the
    /// supplied token are specified by <c>[CMP-20]</c>, <c>[CMP-21]</c>, and <c>[CMP-22]</c>.
    /// </remarks>
    protected void OnUnmounted(Func<CancellationToken, Task> callback) =>
        Context.Lifecycle.OnUnmounted(callback);

    /// <summary>
    /// Registers a callback that captures an error from one of this component's descendants.
    /// </summary>
    /// <param name="callback">
    /// The callback receiving the exception, source component context when available, and diagnostic
    /// information. Returning <see langword="false"/> stops further propagation.
    /// </param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnErrorCaptured(callback)</c>, specified by
    /// <c>[CMP-32]</c>; propagation and the terminal application handler are specified by
    /// <c>[CMP-23]</c>.
    /// </remarks>
    protected void OnErrorCaptured(Func<Exception, IComponentContext?, string, bool> callback) =>
        Context.Lifecycle.OnErrorCaptured(callback);

    /// <summary>Registers a task that server-side rendering awaits before serializing the component.</summary>
    /// <param name="callback">The server-prefetch task factory.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnServerPrefetch(callback)</c>, specified by
    /// <c>[CMP-32]</c>; this is the sole awaited hook, specified by <c>[CMP-21]</c> and
    /// <c>[SSR-4]</c>.
    /// </remarks>
    protected void OnServerPrefetch(Func<Task> callback) => Context.Lifecycle.OnServerPrefetch(callback);

    /// <summary>Registers a task that server-side rendering awaits before serializing the component.</summary>
    /// <param name="callback">
    /// The server-prefetch task factory that receives the component-lifetime token.
    /// </param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnServerPrefetch(callback)</c>, specified by
    /// <c>[CMP-32]</c>; this is the sole awaited hook, specified by <c>[CMP-21]</c> and
    /// <c>[SSR-4]</c>, and the supplied token is specified by <c>[CMP-22]</c>.
    /// </remarks>
    protected void OnServerPrefetch(Func<CancellationToken, Task> callback) =>
        Context.Lifecycle.OnServerPrefetch(callback);

    /// <summary>Registers a synchronous callback that runs when a cached subtree is reactivated.</summary>
    /// <param name="callback">The instance-local callback.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnActivated(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing is specified by <c>[CMP-20]</c> and its child-before-parent
    /// order by <c>[BLT-6]</c>.
    /// </remarks>
    protected void OnActivated(Action callback) => Context.Lifecycle.OnActivated(callback);

    /// <summary>Registers an observed asynchronous callback that starts when a cached subtree is reactivated.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnActivated(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing is specified by <c>[CMP-20]</c> and <c>[BLT-6]</c>, and the
    /// observation of the returned task by <c>[CMP-21]</c>.
    /// </remarks>
    protected void OnActivated(Func<Task> callback) => Context.Lifecycle.OnActivated(callback);

    /// <summary>Registers an observed asynchronous callback that starts when a cached subtree is reactivated.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnActivated(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing is specified by <c>[CMP-20]</c> and <c>[BLT-6]</c>, the
    /// observation of the returned task by <c>[CMP-21]</c>, and the supplied token by <c>[CMP-22]</c>.
    /// </remarks>
    protected void OnActivated(Func<CancellationToken, Task> callback) =>
        Context.Lifecycle.OnActivated(callback);

    /// <summary>Registers a synchronous callback that runs when a cached subtree is deactivated.</summary>
    /// <param name="callback">The instance-local callback.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnDeactivated(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing is specified by <c>[CMP-20]</c> and <c>[BLT-5]</c>.
    /// </remarks>
    protected void OnDeactivated(Action callback) => Context.Lifecycle.OnDeactivated(callback);

    /// <summary>Registers an observed asynchronous callback that starts when a cached subtree is deactivated.</summary>
    /// <param name="callback">The observed instance-local task factory.</param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnDeactivated(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing is specified by <c>[CMP-20]</c> and <c>[BLT-5]</c>, and the
    /// observation of the returned task by <c>[CMP-21]</c>.
    /// </remarks>
    protected void OnDeactivated(Func<Task> callback) => Context.Lifecycle.OnDeactivated(callback);

    /// <summary>Registers an observed asynchronous callback that starts when a cached subtree is deactivated.</summary>
    /// <param name="callback">
    /// The observed instance-local task factory that receives the component-lifetime token.
    /// </param>
    /// <remarks>
    /// The root-level equivalent of <c>Context.Lifecycle.OnDeactivated(callback)</c>, specified by
    /// <c>[CMP-32]</c>; the hook's timing is specified by <c>[CMP-20]</c> and <c>[BLT-5]</c>, the
    /// observation of the returned task by <c>[CMP-21]</c>, and the supplied token by <c>[CMP-22]</c>.
    /// </remarks>
    protected void OnDeactivated(Func<CancellationToken, Task> callback) =>
        Context.Lifecycle.OnDeactivated(callback);
}
