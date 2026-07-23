using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>
/// Optional Pinia-shaped state-store base over one source-generated reactive state object.
/// </summary>
/// <remarks>
/// <para>
/// The lightweight setup style may return any object containing references, computeds, and methods.
/// Derive from this type only when the store needs the richer <see cref="Patch(Action{TState})"/>,
/// <see cref="Reset"/>, <see cref="Subscribe"/>, and <see cref="OnAction"/> member model.
/// </para>
/// <para>
/// State is mutated in place. No state-shape reflection or runtime activation is used. Watches and
/// subscriptions created while the store definition is being initialized are owned by that store's
/// reactive child scope. Not thread-safe; designed for Viu's single-threaded event-loop model.
/// </para>
/// </remarks>
/// <typeparam name="TState">A source-generated reactive state object.</typeparam>
public abstract class StateStore<TState>
    where TState : class, IReactiveObject
{
    private readonly List<StateStoreSubscriptionCallback<TState>> _subscriptions = new();
    private readonly List<StateStoreActionCallback> _actionSubscriptions = new();
    private readonly IReactiveEffectScope? _stateStoreScope;
    private readonly IReactiveWatchScheduler? _watchScheduler;
    private readonly Func<TState>? _initialStateFactory;
    private readonly Action<TState, TState>? _applyState;
    private WatchHandle? _stateWatch;
    private StateStoreWatchScheduler? _observingWatchScheduler;
    private StateStorePatchKind _pendingKind = StateStorePatchKind.Direct;
    private bool _hasPendingNotification;
    private bool _scheduledDuringMutation;

    /// <summary>
    /// Creates a state store over an existing reactive state object.
    /// </summary>
    /// <remarks>
    /// This form supports mutator patches and subscriptions but cannot support object-form
    /// <see cref="Patch(TState)"/> or <see cref="Reset"/> because it has no state factory or typed
    /// state applier.
    /// </remarks>
    /// <param name="key">The state-store key reported to subscribers.</param>
    /// <param name="state">The live reactive state object.</param>
    protected StateStore(string key, TState state)
        : this(
            key,
            state,
            StateStoreSetupRuntime.Current?.WatchScheduler)
    {
    }

    /// <summary>
    /// Creates a state store over an existing reactive state object with an explicit watch
    /// scheduler.
    /// </summary>
    /// <param name="key">The state-store key reported to subscribers.</param>
    /// <param name="state">The live reactive state object.</param>
    /// <param name="watchScheduler">
    /// The watch scheduler, or null for synchronous standalone behavior.
    /// </param>
    protected StateStore(
        string key,
        TState state,
        IReactiveWatchScheduler? watchScheduler)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(state);
        Key = key;
        State = state;
        _stateStoreScope =
            StateStoreSetupRuntime.Current?.Scope
            ?? Reactive.CurrentScope;
        _watchScheduler = watchScheduler;
    }

    /// <summary>
    /// Creates a resettable state store from a state factory and a typed in-place state applier.
    /// </summary>
    /// <param name="key">The state-store key reported to subscribers.</param>
    /// <param name="stateFactory">Creates the initial state and a fresh reset state.</param>
    /// <param name="applyState">
    /// Copies the second state object's values onto the first state object without reflection.
    /// </param>
    protected StateStore(
        string key,
        Func<TState> stateFactory,
        Action<TState, TState> applyState)
        : this(
            key,
            stateFactory,
            applyState,
            StateStoreSetupRuntime.Current?.WatchScheduler)
    {
    }

    /// <summary>
    /// Creates a resettable state store with an explicit watch scheduler.
    /// </summary>
    /// <param name="key">The state-store key reported to subscribers.</param>
    /// <param name="stateFactory">Creates the initial state and a fresh reset state.</param>
    /// <param name="applyState">
    /// Copies the second state object's values onto the first state object without reflection.
    /// </param>
    /// <param name="watchScheduler">
    /// The watch scheduler, or null for synchronous standalone behavior.
    /// </param>
    protected StateStore(
        string key,
        Func<TState> stateFactory,
        Action<TState, TState> applyState,
        IReactiveWatchScheduler? watchScheduler)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(stateFactory);
        ArgumentNullException.ThrowIfNull(applyState);
        Key = key;
        State = stateFactory()
            ?? throw new ArgumentNullException(
                nameof(stateFactory),
                "The state factory returned null.");
        _initialStateFactory = stateFactory;
        _applyState = applyState;
        _stateStoreScope =
            StateStoreSetupRuntime.Current?.Scope
            ?? Reactive.CurrentScope;
        _watchScheduler = watchScheduler;
    }

    /// <summary>Gets the state-store key reported to subscribers.</summary>
    public string Key { get; }

    /// <summary>
    /// Gets the stable reactive state object. Patch and reset operations mutate this object in
    /// place rather than replacing it.
    /// </summary>
    public TState State { get; }

    /// <summary>
    /// Applies a group of state changes as one <see cref="StateStorePatchKind.PatchFunction"/>
    /// mutation.
    /// </summary>
    /// <param name="mutator">The state mutator.</param>
    public void Patch(Action<TState> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        ApplyMutations(StateStorePatchKind.PatchFunction, mutator);
    }

    /// <summary>
    /// Copies a typed state object onto the live state as one
    /// <see cref="StateStorePatchKind.PatchObject"/> mutation.
    /// </summary>
    /// <param name="partialState">The source state supplied to the configured state applier.</param>
    /// <exception cref="NotSupportedException">
    /// The state store was constructed without a state applier.
    /// </exception>
    public void Patch(TState partialState)
    {
        ArgumentNullException.ThrowIfNull(partialState);
        Action<TState, TState> applyState = _applyState
            ?? throw new NotSupportedException(
                $"State store \"{Key}\" does not support object-form Patch. Construct it with "
                + "the state-factory and state-applier constructor or use Patch(Action<TState>).");
        ApplyMutations(
            StateStorePatchKind.PatchObject,
            state => applyState(state, partialState));
    }

    /// <summary>
    /// Restores the live state in place from a fresh state-factory result.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The state store was constructed without a state factory and state applier.
    /// </exception>
    public void Reset()
    {
        if (_initialStateFactory is null || _applyState is null)
        {
            throw new NotSupportedException(
                $"State store \"{Key}\" does not implement Reset because it was constructed "
                + "without a state factory and state applier.");
        }

        Func<TState> stateFactory = _initialStateFactory;
        Action<TState, TState> applyState = _applyState;
        ApplyMutations(
            StateStorePatchKind.PatchFunction,
            state => applyState(state, stateFactory()));
    }

    /// <summary>
    /// Subscribes to state changes. With an application scheduler, changes are deduplicated per
    /// flush; without one, standalone Reactivity delivers synchronously.
    /// </summary>
    /// <param name="callback">The state mutation callback.</param>
    /// <param name="detached">
    /// Whether the subscription should survive disposal of the current caller scope.
    /// </param>
    /// <returns>A removable subscription.</returns>
    public StateStoreSubscription Subscribe(
        StateStoreSubscriptionCallback<TState> callback,
        bool detached = false)
    {
        ArgumentNullException.ThrowIfNull(callback);
        EnsureStateWatch();
        _subscriptions.Add(callback);
        StateStoreSubscription subscription = new(
            () => _subscriptions.Remove(callback));
        RegisterAutomaticRemoval(subscription, detached);
        return subscription;
    }

    /// <summary>Subscribes to actions routed through the protected action helpers.</summary>
    /// <param name="callback">The action callback invoked before each observed action body.</param>
    /// <param name="detached">
    /// Whether the subscription should survive disposal of the current caller scope.
    /// </param>
    /// <returns>A removable subscription.</returns>
    public StateStoreSubscription OnAction(
        StateStoreActionCallback callback,
        bool detached = false)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _actionSubscriptions.Add(callback);
        StateStoreSubscription subscription = new(
            () => _actionSubscriptions.Remove(callback));
        RegisterAutomaticRemoval(subscription, detached);
        return subscription;
    }

    /// <summary>Runs a void action under action-subscription observation.</summary>
    /// <param name="name">The action name.</param>
    /// <param name="body">The action body.</param>
    protected void RunAction(string name, Action body)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(body);
        if (_actionSubscriptions.Count == 0)
        {
            body();
            return;
        }

        StateStoreActionContext context = BeginAction(name);
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

    /// <summary>Runs a value-returning action under action-subscription observation.</summary>
    /// <typeparam name="TResult">The action result type.</typeparam>
    /// <param name="name">The action name.</param>
    /// <param name="body">The action body.</param>
    /// <returns>The action result.</returns>
    protected TResult RunAction<TResult>(string name, Func<TResult> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(body);
        if (_actionSubscriptions.Count == 0)
        {
            return body();
        }

        StateStoreActionContext context = BeginAction(name);
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

    /// <summary>Runs and awaits an asynchronous action under action-subscription observation.</summary>
    /// <param name="name">The action name.</param>
    /// <param name="body">The asynchronous action body.</param>
    /// <returns>A task completing after the action and completion hooks.</returns>
    protected async Task RunActionAsync(string name, Func<Task> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(body);
        if (_actionSubscriptions.Count == 0)
        {
            await body().ConfigureAwait(false);
            return;
        }

        StateStoreActionContext context = BeginAction(name);
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
    /// Runs and awaits a value-returning asynchronous action under action-subscription observation.
    /// </summary>
    /// <typeparam name="TResult">The resolved action result type.</typeparam>
    /// <param name="name">The action name.</param>
    /// <param name="body">The asynchronous action body.</param>
    /// <returns>A task producing the resolved action result.</returns>
    protected async Task<TResult> RunActionAsync<TResult>(
        string name,
        Func<Task<TResult>> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(body);
        if (_actionSubscriptions.Count == 0)
        {
            return await body().ConfigureAwait(false);
        }

        StateStoreActionContext context = BeginAction(name);
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

    private StateStoreActionContext BeginAction(string name)
    {
        StateStoreActionContext context = new(name, this);
        StateStoreActionCallback[] subscribers = _actionSubscriptions.ToArray();
        foreach (StateStoreActionCallback subscriber in subscribers)
        {
            subscriber(context);
        }

        return context;
    }

    private void ApplyMutations(
        StateStorePatchKind kind,
        Action<TState> mutator)
    {
        if (_stateWatch is not null)
        {
            _pendingKind = kind;
            _scheduledDuringMutation = false;
        }

        Reactive.StartBatch();
        try
        {
            mutator(State);
        }
        finally
        {
            try
            {
                Reactive.EndBatch();
            }
            finally
            {
                if (_stateWatch is not null
                    && !_scheduledDuringMutation
                    && !_hasPendingNotification)
                {
                    _pendingKind = StateStorePatchKind.Direct;
                }
            }
        }
    }

    private void EnsureStateWatch()
    {
        if (_stateWatch is not null)
        {
            return;
        }

        _pendingKind = StateStorePatchKind.Direct;
        if (_stateStoreScope is { IsActive: true })
        {
            _stateStoreScope.Run(CreateStateWatch);
            return;
        }

        CreateStateWatch();
    }

    private void CreateStateWatch()
    {
        WatchOptions? options = null;
        if (_watchScheduler is not null)
        {
            _observingWatchScheduler = new StateStoreWatchScheduler(
                _watchScheduler,
                OnWatchScheduled);
            options = new WatchOptions
            {
                Flush = WatchFlushMode.Pre,
                Scheduler = _observingWatchScheduler,
            };
        }

        _stateWatch = Reactive.Watch(State, OnStateChanged, options);
    }

    private void OnWatchScheduled()
    {
        _scheduledDuringMutation = true;
        _hasPendingNotification = true;
    }

    private void OnStateChanged(
        TState state,
        TState previousState,
        OnCleanup onCleanup)
    {
        StateStorePatchKind kind = _pendingKind;
        _pendingKind = StateStorePatchKind.Direct;
        _hasPendingNotification = false;
        if (_subscriptions.Count == 0)
        {
            return;
        }

        StateStoreMutation mutation = new(Key, kind);
        StateStoreSubscriptionCallback<TState>[] subscribers =
            _subscriptions.ToArray();
        foreach (StateStoreSubscriptionCallback<TState> subscriber in subscribers)
        {
            subscriber(mutation, state);
        }
    }

    private static void RegisterAutomaticRemoval(
        StateStoreSubscription subscription,
        bool detached)
    {
        if (detached)
        {
            return;
        }

        if (Reactive.CurrentScope is { IsActive: true })
        {
            Reactive.OnScopeDispose(
                subscription.Stop,
                failSilently: true);
        }
    }
}
