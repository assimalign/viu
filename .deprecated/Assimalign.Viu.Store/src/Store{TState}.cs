using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu;

namespace Assimalign.Viu.Store;

/// <summary>
/// The optional base class that gives a setup-style store Pinia's state-management instance API on top
/// of a source-generated <c>[Reactive]</c> state object — the C# port of the state/getters/actions
/// model and the <c>$patch</c>/<c>$reset</c>/<c>$subscribe</c>/<c>$onAction</c> surface a Pinia store
/// exposes (https://pinia.vuejs.org/core-concepts/, <c>packages/pinia/src/store.ts</c>).
/// <para>
/// A store deriving from <see cref="Store{TState}"/> holds its reactive state as
/// <typeparamref name="TState"/> (an <see cref="IReactiveObject"/> from the Roslyn <c>[Reactive]</c>
/// generator, giving per-member track/trigger). Getters are ordinary <see cref="Computed{T}"/> members
/// read off <see cref="State"/>, so a getter recomputes only when the specific state member it read
/// changes (upstream <c>computed()</c> caching). Actions are ordinary methods; route a body through
/// <see cref="RunAction(string, Action)"/> (or an overload) for it to be observable by
/// <see cref="OnAction"/>.
/// </para>
/// <para>
/// Subscriptions ride the runtime scheduler's microtask-batched flush: a direct member write and a
/// grouped <see cref="Patch(Action{TState})"/> each notify <see cref="Subscribe"/> callbacks exactly
/// once per flush — a patch of N members costs one notification pass, not N — which on WASM bounds the
/// downstream JS-interop a subscriber triggers. Every computed, watcher, and subscription created here
/// is owned by the store's <see cref="EffectScope"/>, so disposing the store (or its app/registry)
/// tears them all down. Trimming- and NativeAOT-safe: state comes from the source generator and every
/// hook is a typed delegate — no reflection, no <c>Proxy</c>, no dynamic code. Not thread-safe
/// (single-threaded JS event-loop model).
/// </para>
/// </summary>
/// <typeparam name="TState">The store's reactive state type (a <c>[Reactive]</c> source-generated object).</typeparam>
public abstract class Store<TState>
    where TState : class, IReactiveObject
{
    private readonly List<StoreSubscriptionCallback<TState>> _subscriptions = new();
    private readonly List<StoreActionCallback> _actionSubscriptions = new();
    private readonly EffectScope? _storeScope;
    private readonly Func<TState>? _initialStateFactory;
    private readonly Action<TState, TState>? _applyState;
    private WatchHandle? _stateWatch;
    private SchedulerJob? _resetKindJob;
    private StorePatchKind _pendingKind = StorePatchKind.Direct;

    /// <summary>
    /// Creates a store whose state is the supplied <paramref name="state"/> instance. This overload
    /// supports the mutator-delegate <see cref="Patch(Action{TState})"/>, <see cref="Subscribe"/>, and
    /// <see cref="OnAction"/>, but not the partial-state <see cref="Patch(TState)"/> or
    /// <see cref="Reset"/> (both need a state applier — use the other constructor). Mirrors a Pinia
    /// setup store, which likewise does not implement <c>$reset</c> unless given explicit support.
    /// </summary>
    /// <param name="id">The store's id, reported to subscribers in <see cref="StoreMutation.StoreId"/> (match the <c>DefineStore</c> id).</param>
    /// <param name="state">The reactive state object.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    protected Store(string id, TState state)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(state);
        Id = id;
        State = state;
        _storeScope = Reactive.CurrentScope;
    }

    /// <summary>
    /// Creates a store whose initial state comes from <paramref name="stateFactory"/> and that can be
    /// reset and object-patched. <paramref name="applyState"/> copies a source state onto the live
    /// state in place (so <see cref="State"/> keeps its identity and per-member reactivity):
    /// <see cref="Patch(TState)"/> applies a caller-supplied partial, and <see cref="Reset"/> applies a
    /// fresh <paramref name="stateFactory"/> instance — the C# stand-in for a Pinia option store's
    /// <c>state()</c> factory (which is what makes <c>$reset</c> available). Both are trimming-safe:
    /// <paramref name="applyState"/> is a typed field-copy the store author writes, never reflection.
    /// </summary>
    /// <param name="id">The store's id, reported to subscribers in <see cref="StoreMutation.StoreId"/> (match the <c>DefineStore</c> id).</param>
    /// <param name="stateFactory">Produces the initial state (invoked now) and each reset state.</param>
    /// <param name="applyState">Copies the source state (second argument) onto the live state (first argument), in place.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="stateFactory"/> or <paramref name="applyState"/> is null, or <paramref name="stateFactory"/> returns null.</exception>
    protected Store(string id, Func<TState> stateFactory, Action<TState, TState> applyState)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(stateFactory);
        ArgumentNullException.ThrowIfNull(applyState);
        Id = id;
        State = stateFactory() ?? throw new ArgumentNullException(nameof(stateFactory), "The state factory returned null.");
        _initialStateFactory = stateFactory;
        _applyState = applyState;
        _storeScope = Reactive.CurrentScope;
    }

    /// <summary>The store's unique id, reported to subscribers (upstream: the store's <c>$id</c>).</summary>
    public string Id { get; }

    /// <summary>
    /// The store's reactive state — the C# port of Pinia's <c>store.$state</c>
    /// (https://pinia.vuejs.org/core-concepts/state.html). Read members off it in getters (they track
    /// per member) and write them in actions (each write triggers). The instance is stable across the
    /// store's life; <see cref="Reset"/> and <see cref="Patch(TState)"/> mutate it in place rather than
    /// replacing it.
    /// </summary>
    public TState State { get; }

    /// <summary>
    /// Applies a group of mutations through <paramref name="mutator"/> and notifies
    /// <see cref="Subscribe"/> callbacks exactly once for the whole group (not once per member write) —
    /// the mutator-delegate form of Pinia's <c>$patch(state =&gt; { ... })</c>
    /// (https://pinia.vuejs.org/core-concepts/state.html#Mutating-the-state,
    /// <c>packages/pinia/src/store.ts</c>). The writes are batched so downstream computeds and effects
    /// also coalesce, and the single subscriber notification carries
    /// <see cref="StorePatchKind.PatchFunction"/>.
    /// </summary>
    /// <param name="mutator">Receives <see cref="State"/> and mutates its members.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mutator"/> is null.</exception>
    public void Patch(Action<TState> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        ApplyMutations(StorePatchKind.PatchFunction, mutator);
    }

    /// <summary>
    /// Applies <paramref name="partialState"/> onto <see cref="State"/> (via the constructor's state
    /// applier) and notifies subscribers once with <see cref="StorePatchKind.PatchObject"/> — the
    /// partial-state form of Pinia's <c>$patch({ ... })</c>. The applier decides which members to copy,
    /// so this is a typed, reflection-free merge; for a genuinely sparse update prefer the mutator form
    /// <see cref="Patch(Action{TState})"/>, which touches only the members it assigns.
    /// </summary>
    /// <param name="partialState">The source state whose members the applier copies onto <see cref="State"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="partialState"/> is null.</exception>
    /// <exception cref="NotSupportedException">The store was created without a state applier (use the factory/applier constructor).</exception>
    public void Patch(TState partialState)
    {
        ArgumentNullException.ThrowIfNull(partialState);
        var applyState = _applyState
            ?? throw new NotSupportedException(
                $"Store \"{Id}\" does not support object-form Patch: construct it with the "
                + "(id, stateFactory, applyState) constructor to supply a state applier, or use the "
                + "mutator form Patch(Action<TState>).");
        ApplyMutations(StorePatchKind.PatchObject, state => applyState(state, partialState));
    }

    /// <summary>
    /// Restores <see cref="State"/> to a fresh instance from the constructor's state factory and
    /// notifies subscribers once — the C# port of Pinia's <c>store.$reset()</c>
    /// (https://pinia.vuejs.org/core-concepts/state.html#Resetting-the-state). Parity note: like a
    /// Pinia setup store, a store built without an initial-state factory does not implement reset and
    /// this method throws.
    /// </summary>
    /// <exception cref="NotSupportedException">The store was created without a state factory/applier (use the factory/applier constructor).</exception>
    public void Reset()
    {
        if (_initialStateFactory is null || _applyState is null)
        {
            throw new NotSupportedException(
                $"Store \"{Id}\" does not implement Reset: it was built without an initial-state "
                + "factory. Construct it with the (id, stateFactory, applyState) constructor to enable "
                + "Reset (parity with Pinia setup stores, which do not implement $reset by default).");
        }
        var applyState = _applyState;
        var factory = _initialStateFactory;
        ApplyMutations(StorePatchKind.PatchFunction, state => applyState(state, factory()));
    }

    /// <summary>
    /// Registers <paramref name="callback"/> to observe state changes — the C# port of Pinia's
    /// <c>store.$subscribe(callback, { detached })</c>
    /// (https://pinia.vuejs.org/core-concepts/state.html#Subscribing-to-the-state). The callback fires
    /// once per scheduler flush with the <see cref="StoreMutation"/> metadata and current
    /// <see cref="State"/>. When called inside an active <see cref="EffectScope"/> (for example a
    /// component's <c>Setup</c>), the subscription is removed automatically when that scope stops,
    /// unless <paramref name="detached"/> is <see langword="true"/> (upstream detached semantics).
    /// </summary>
    /// <param name="callback">Invoked with <c>(mutation, state)</c> after a change.</param>
    /// <param name="detached">When true, the subscription survives the calling scope's disposal.</param>
    /// <returns>A handle that removes the subscription when stopped or disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is null.</exception>
    public StoreSubscription Subscribe(StoreSubscriptionCallback<TState> callback, bool detached = false)
    {
        ArgumentNullException.ThrowIfNull(callback);
        EnsureStateWatch();
        _subscriptions.Add(callback);
        var subscription = new StoreSubscription(() => _subscriptions.Remove(callback));
        RegisterAutomaticRemoval(subscription, detached);
        return subscription;
    }

    /// <summary>
    /// Registers <paramref name="callback"/> to observe action invocations — the C# port of Pinia's
    /// <c>store.$onAction(callback, detached)</c>
    /// (https://pinia.vuejs.org/core-concepts/actions.html#Subscribing-to-actions). It fires before
    /// each action (routed through <see cref="RunAction(string, Action)"/> and overloads) runs and may
    /// register <see cref="StoreActionContext.After"/> / <see cref="StoreActionContext.OnError"/> hooks.
    /// Like <see cref="Subscribe"/>, a non-<paramref name="detached"/> subscription created inside an
    /// active scope is removed when that scope stops.
    /// </summary>
    /// <param name="callback">Invoked with the action <see cref="StoreActionContext"/> before the body runs.</param>
    /// <param name="detached">When true, the subscription survives the calling scope's disposal.</param>
    /// <returns>A handle that removes the subscription when stopped or disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is null.</exception>
    public StoreSubscription OnAction(StoreActionCallback callback, bool detached = false)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _actionSubscriptions.Add(callback);
        var subscription = new StoreSubscription(() => _actionSubscriptions.Remove(callback));
        RegisterAutomaticRemoval(subscription, detached);
        return subscription;
    }

    /// <summary>
    /// Runs a void action body under <see cref="OnAction"/> observation: fires the action subscribers,
    /// runs <paramref name="body"/>, then the registered <c>after</c> hooks (with a
    /// <see langword="null"/> result), or the <c>onError</c> hooks if it throws (then rethrows).
    /// </summary>
    /// <param name="name">The action name reported to subscribers.</param>
    /// <param name="body">The action body.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> is null.</exception>
    protected void RunAction(string name, Action body)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(body);
        if (_actionSubscriptions.Count == 0)
        {
            body();
            return;
        }
        var context = BeginAction(name);
        try
        {
            body();
        }
        catch (Exception exception)
        {
            context.RunError(exception);
            throw;
        }
        context.RunAfter(null);
    }

    /// <summary>
    /// Runs a value-returning action body under <see cref="OnAction"/> observation and returns its
    /// result; the <c>after</c> hooks receive that result (boxed), or the <c>onError</c> hooks the
    /// thrown exception.
    /// </summary>
    /// <typeparam name="TResult">The action's return type.</typeparam>
    /// <param name="name">The action name reported to subscribers.</param>
    /// <param name="body">The action body.</param>
    /// <returns>The value returned by <paramref name="body"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> is null.</exception>
    protected TResult RunAction<TResult>(string name, Func<TResult> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(body);
        if (_actionSubscriptions.Count == 0)
        {
            return body();
        }
        var context = BeginAction(name);
        TResult result;
        try
        {
            result = body();
        }
        catch (Exception exception)
        {
            context.RunError(exception);
            throw;
        }
        context.RunAfter(result);
        return result;
    }

    /// <summary>
    /// Runs an asynchronous action body under <see cref="OnAction"/> observation, awaiting it so the
    /// <c>after</c> hooks fire on resolution (with a <see langword="null"/> result) and the
    /// <c>onError</c> hooks on a faulted task — the async parity of upstream <c>$onAction</c>.
    /// </summary>
    /// <param name="name">The action name reported to subscribers.</param>
    /// <param name="body">The asynchronous action body.</param>
    /// <returns>A task that completes after the action and its <c>after</c> hooks run.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> is null.</exception>
    protected async Task RunActionAsync(string name, Func<Task> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(body);
        if (_actionSubscriptions.Count == 0)
        {
            await body().ConfigureAwait(false);
            return;
        }
        var context = BeginAction(name);
        try
        {
            await body().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            context.RunError(exception);
            throw;
        }
        context.RunAfter(null);
    }

    /// <summary>
    /// Runs a value-returning asynchronous action body under <see cref="OnAction"/> observation,
    /// awaiting it so the <c>after</c> hooks receive the <em>resolved</em> result (boxed) and the
    /// <c>onError</c> hooks a faulted task's exception.
    /// </summary>
    /// <typeparam name="TResult">The action's resolved result type.</typeparam>
    /// <param name="name">The action name reported to subscribers.</param>
    /// <param name="body">The asynchronous action body.</param>
    /// <returns>A task producing the value the action resolved to.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> is null.</exception>
    protected async Task<TResult> RunActionAsync<TResult>(string name, Func<Task<TResult>> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(body);
        if (_actionSubscriptions.Count == 0)
        {
            return await body().ConfigureAwait(false);
        }
        var context = BeginAction(name);
        TResult result;
        try
        {
            result = await body().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            context.RunError(exception);
            throw;
        }
        context.RunAfter(result);
        return result;
    }

    // Fires the action subscribers with a fresh context (upstream: triggerSubscriptions before the
    // action runs) so each may register after/onError hooks. Snapshotted so a subscriber that
    // (un)subscribes during the callback cannot corrupt the iteration.
    private StoreActionContext BeginAction(string name)
    {
        var context = new StoreActionContext(name, this);
        var subscribers = _actionSubscriptions.ToArray();
        foreach (var subscriber in subscribers)
        {
            subscriber(context);
        }
        return context;
    }

    // Runs the mutator with the pending patch kind tagged for the state watcher, batching the writes
    // so N member changes coalesce into one downstream flush (upstream $patch wraps the mutation and
    // triggers a single subscription pass). The kind is only tagged when a watcher exists, so a patch
    // made before the first Subscribe leaves no stale kind behind.
    private void ApplyMutations(StorePatchKind kind, Action<TState> mutator)
    {
        if (_stateWatch is not null)
        {
            _pendingKind = kind;
            // The pre-flush watcher clears the kind when it fires, but a patch that changes nothing
            // never fires it; queue a post-flush reset (it runs after the notification pass) so the
            // tag can never leak onto a later direct write.
            Scheduler.QueuePostFlushCallback(_resetKindJob ??= new SchedulerJob(ResetPendingKind));
        }
        Reactive.StartBatch();
        try
        {
            mutator(State);
        }
        finally
        {
            Reactive.EndBatch();
        }
    }

    private void ResetPendingKind() => _pendingKind = StorePatchKind.Direct;

    // Lazily creates the single fan-out state watcher inside the store's own scope (so it is torn down
    // with the store, never captured by whichever scope first subscribes). Pre-flush + runtime
    // scheduler: several triggers in one turn dedupe to a single notification pass.
    private void EnsureStateWatch()
    {
        if (_stateWatch is not null)
        {
            return;
        }
        _pendingKind = StorePatchKind.Direct;
        if (_storeScope is { IsActive: true })
        {
            _storeScope.Run(CreateStateWatch);
        }
        else
        {
            CreateStateWatch();
        }
    }

    private void CreateStateWatch() => _stateWatch = ViuWatch.Watch<TState>(State, OnStateChanged);

    private void OnStateChanged(TState state, TState previousState, OnCleanup onCleanup)
    {
        var kind = _pendingKind;
        _pendingKind = StorePatchKind.Direct;
        if (_subscriptions.Count == 0)
        {
            return;
        }
        var mutation = new StoreMutation(Id, kind);
        // Snapshot so a callback that (un)subscribes during notification cannot corrupt iteration.
        var subscribers = _subscriptions.ToArray();
        foreach (var subscriber in subscribers)
        {
            subscriber(mutation, state);
        }
    }

    // Upstream addSubscription: when not detached and created inside an active scope, remove the
    // subscription when that scope stops (onScopeDispose). A detached subscription, or one created
    // with no active scope, must be stopped explicitly.
    private static void RegisterAutomaticRemoval(StoreSubscription subscription, bool detached)
    {
        if (detached)
        {
            return;
        }
        if (Reactive.CurrentScope is { IsActive: true })
        {
            Reactive.OnScopeDispose(subscription.Stop, failSilently: true);
        }
    }
}
