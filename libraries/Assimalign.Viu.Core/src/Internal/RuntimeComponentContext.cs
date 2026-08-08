using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

internal sealed class RuntimeComponentContext : ComponentContext, IAsynchronousComponentRuntime
{
    private static RuntimeComponentContext? _current;

    private readonly ComponentContract _contract;
    private readonly Dictionary<string, ComponentEvent> _eventAliases =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _defaultValues =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _emittedOnceListeners =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _reportedBindingDiagnostics =
        new(StringComparer.Ordinal);
    private readonly Action<Exception, ComponentContext?, string>? _errorHandler;
    private readonly Action<string>? _warnHandler;
    private readonly Action<ComponentContext, string, IReadOnlyList<object?>>? _eventObserver;
    private ComponentBindings _bindings;
    private IReadOnlyDictionary<string, ComponentEventListener> _listeners;
    private List<Task>? _taskObservers;
    private object? _exposedValue;

    internal RuntimeComponentContext(
        ComponentContract contract,
        ComponentInvocation invocation,
        MountReference? mountReference,
        IServiceProvider? services,
        ComponentLifecycle lifecycle,
        IReactiveEffectScope scope,
        IReactiveWatchScheduler? watchScheduler,
        ComponentContext? parent,
        Action<Exception, ComponentContext?, string>? errorHandler,
        Action<string>? warnHandler,
        Action<ComponentContext, string, IReadOnlyList<object?>>? eventObserver)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(scope);

        _contract = contract;
        _listeners = invocation.Listeners;
        _errorHandler = errorHandler;
        _warnHandler = warnHandler;
        _eventObserver = eventObserver;
        Invocation = invocation;
        MountReference = mountReference;
        Services = services;
        Lifecycle = lifecycle;
        Scope = scope;
        WatchScheduler = watchScheduler;
        Parent = parent;
        SuspenseBoundary = (parent as RuntimeComponentContext)?.SuspenseBoundary;
        _bindings = new ComponentBindings();

        foreach (ComponentEvent componentEvent in contract.Events)
        {
            AddEventAlias(componentEvent.Name, componentEvent);
            AddEventAlias(NameNormalization.Camelize(componentEvent.Name), componentEvent);
            AddEventAlias(NameNormalization.Hyphenate(componentEvent.Name), componentEvent);
        }

    }

    internal static RuntimeComponentContext? Current => _current;

    public override ComponentBindings Bindings => _bindings;

    public override IServiceProvider? Services { get; }

    public override ComponentLifecycle Lifecycle { get; }

    public override IReactiveEffectScope Scope { get; }

    public override IReactiveWatchScheduler? WatchScheduler { get; }

    public override ComponentContext? Parent { get; }

    internal object? ExposedValue => _exposedValue;

    internal bool HasExposedValue { get; private set; }

    internal ComponentInvocation Invocation { get; private set; }

    internal SlotStability EffectiveSlotStability { get; private set; } = SlotStability.Stable;

    ComponentInvocation IAsynchronousComponentRuntime.Invocation => Invocation;

    internal MountReference? MountReference { get; private set; }

    MountReference? IAsynchronousComponentRuntime.MountReference => MountReference;

    internal SuspenseBoundary? SuspenseBoundary { get; set; }

    public override void Emit(string name, params object?[] arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(arguments);

        IReadOnlyList<object?> eventArguments = Array.AsReadOnly(arguments);
        ComponentEvent? declaration = ResolveEvent(name);
        if (_contract.Events.Count > 0 && declaration is null)
        {
            Warn($"Component emitted undeclared event '{name}'.");
        }
        else if (declaration?.Validator is not null)
        {
            bool isValid;
            try
            {
                isValid = Run(() => declaration.Validator(eventArguments));
            }
            catch (Exception exception)
            {
                RouteError(exception, $"component event validator '{name}'");
                return;
            }

            if (!isValid)
            {
                Warn($"Invalid arguments were emitted for component event '{name}'.");
            }
        }

        InvokeListener(name, eventArguments, isOnceName: false);
        InvokeListener(name, eventArguments, isOnceName: true);

        if (_eventObserver is not null)
        {
            try
            {
                Run(() => _eventObserver(this, name, eventArguments));
            }
            catch (Exception exception)
            {
                RouteError(exception, "application event observer");
            }
        }
    }

    public override void Expose(object? value)
    {
        HasExposedValue = true;
        _exposedValue = value;
    }

    public override void Warn(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        _warnHandler?.Invoke(message);
    }

    internal void Update(ComponentInvocation invocation, bool isInitial = false)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        Invocation = invocation;
        EffectiveSlotStability = ResolveEffectiveSlotStability(invocation.SlotStability);

        List<ComponentBindingDiagnostic> diagnostics = [];
        ComponentBindings resolved = Run(
            () => ComponentBindings.Resolve(_contract, invocation, diagnostics));
        Dictionary<string, object?> parameters = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> parameter in resolved.Parameters)
        {
            parameters.Add(parameter.Key, parameter.Value);
        }

        foreach (ComponentParameter parameter in _contract.Parameters)
        {
            if (parameters.ContainsKey(parameter.Name)
                || parameter.DefaultFactory is null)
            {
                continue;
            }

            if (!_defaultValues.TryGetValue(parameter.Name, out object? defaultValue))
            {
                defaultValue = Run(parameter.DefaultFactory);
                _defaultValues.Add(parameter.Name, defaultValue);
            }

            parameters.Add(parameter.Name, defaultValue);
            if (parameter.Validator is not null
                && !Run(() => parameter.Validator(defaultValue)))
            {
                diagnostics.Add(
                    new ComponentBindingDiagnostic(
                        ComponentBindingDiagnosticKind.ParameterValidationFailed,
                        parameter.Name,
                        "Invalid default value was produced for component parameter "
                            + $"'{parameter.Name}'."));
            }
        }

        _bindings = new ComponentBindings(
            parameters,
            resolved.Slots,
            resolved.FallthroughBindings);
        _listeners = invocation.Listeners;

        foreach (ComponentBindingDiagnostic diagnostic in diagnostics)
        {
            if (!isInitial
                && diagnostic.Kind == ComponentBindingDiagnosticKind.MissingRequiredParameter)
            {
                continue;
            }

            string diagnosticKey = string.Concat(
                ((int)diagnostic.Kind).ToString(CultureInfo.InvariantCulture),
                "\u001f",
                diagnostic.Name,
                "\u001f",
                diagnostic.Message);
            if (_reportedBindingDiagnostics.Add(diagnosticKey))
            {
                Warn(diagnostic.Message);
            }
        }
    }

    internal void UpdateMountReference(MountReference? mountReference) =>
        MountReference = mountReference;

    private SlotStability ResolveEffectiveSlotStability(SlotStability slotStability) => slotStability switch
    {
        SlotStability.Stable => SlotStability.Stable,
        SlotStability.Dynamic => SlotStability.Dynamic,
        SlotStability.Forwarded => (Parent as RuntimeComponentContext)?.EffectiveSlotStability
            == SlotStability.Stable
                ? SlotStability.Stable
                : SlotStability.Dynamic,
        _ => throw new ArgumentOutOfRangeException(nameof(slotStability)),
    };

    internal void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        RuntimeComponentContext? previous = _current;
        _current = this;
        try
        {
            action();
        }
        finally
        {
            _current = previous;
        }
    }

    internal TResult Run<TResult>(Func<TResult> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        RuntimeComponentContext? previous = _current;
        _current = this;
        try
        {
            return function();
        }
        finally
        {
            _current = previous;
        }
    }

    internal void ObserveTask(
        Task? task,
        string diagnosticInformation,
        bool rethrowIfUnhandled = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(diagnosticInformation);
        Task observer = ObserveTaskCoreAsync(
            task,
            diagnosticInformation,
            rethrowIfUnhandled);
        (_taskObservers ??= []).Add(observer);
    }

    public bool RegisterAsynchronousDependency(
        Task dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        if (SuspenseBoundary is not null)
        {
            SuspenseBoundary.Register(dependency);
            return true;
        }

        return false;
    }

    public void SettleAsynchronousDependency(Task dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        SuspenseBoundary?.Settle(dependency);
    }

    public void RouteAsynchronousError(
        Exception exception,
        bool rethrowIfUnhandled)
    {
        try
        {
            RouteError(
                exception,
                "asynchronous component loader",
                rethrowIfUnhandled);
        }
        catch (Exception unhandled)
        {
            ExceptionDispatchInfo captured = ExceptionDispatchInfo.Capture(unhandled);
            Scheduler.QueuePostFlushCallback(
                new SchedulerJob(captured.Throw)
                {
                    Name = "asynchronous component loader",
                });
        }
    }

    internal async Task DrainObservedTasksAsync()
    {
        while (_taskObservers is { Count: > 0 })
        {
            Task[] observers = _taskObservers.ToArray();
            _taskObservers.Clear();
            await Task.WhenAll(observers).ConfigureAwait(false);
        }
    }

    internal void RouteError(
        Exception exception,
        string diagnosticInformation,
        bool rethrowIfUnhandled = true)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrEmpty(diagnosticInformation);

        ComponentContext? source = this;
        for (ComponentContext? ancestor = Parent;
            ancestor is not null;
            ancestor = ancestor.Parent)
        {
            try
            {
                bool shouldContinue = ancestor is RuntimeComponentContext runtimeAncestor
                    ? runtimeAncestor.Run(
                        () => ancestor.Lifecycle.InvokeErrorCaptured(
                            exception,
                            source,
                            diagnosticInformation))
                    : ancestor.Lifecycle.InvokeErrorCaptured(
                        exception,
                        source,
                        diagnosticInformation);
                if (!shouldContinue)
                {
                    return;
                }
            }
            catch (Exception captureException)
            {
                exception = captureException;
                source = ancestor;
            }
        }

        if (_errorHandler is not null)
        {
            Run(() => _errorHandler(exception, source, diagnosticInformation));
            return;
        }

        if (rethrowIfUnhandled)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    protected override void OnWatchError(Exception exception) =>
        RouteError(exception, "component watch callback");

    private ComponentEvent? ResolveEvent(string name)
    {
        if (_eventAliases.TryGetValue(name, out ComponentEvent? declaration))
        {
            return declaration;
        }

        string camelizedName = NameNormalization.Camelize(name);
        return _eventAliases.TryGetValue(camelizedName, out declaration)
            ? declaration
            : null;
    }

    private void AddEventAlias(string alias, ComponentEvent componentEvent)
    {
        if (alias.Length > 0)
        {
            _eventAliases.TryAdd(alias, componentEvent);
        }
    }

    private void InvokeListener(
        string eventName,
        IReadOnlyList<object?> arguments,
        bool isOnceName)
    {
        if (!TryGetListener(
            eventName,
            isOnceName,
            out string? listenerName,
            out ComponentEventListener? listener))
        {
            return;
        }

        if (isOnceName && !_emittedOnceListeners.Add(listenerName))
        {
            return;
        }

        try
        {
            Run(() => listener(arguments));
        }
        catch (Exception exception)
        {
            RouteError(exception, $"component event listener \"{eventName}\"");
        }
    }

    private bool TryGetListener(
        string eventName,
        bool isOnceName,
        out string listenerName,
        out ComponentEventListener listener)
    {
        string suffix = isOnceName ? "Once" : string.Empty;
        string exactName = string.Concat(eventName, suffix);
        if (_listeners.TryGetValue(exactName, out listener!))
        {
            listenerName = exactName;
            return true;
        }

        string camelizedName = string.Concat(NameNormalization.Camelize(eventName), suffix);
        if (!string.Equals(camelizedName, exactName, StringComparison.Ordinal)
            && _listeners.TryGetValue(camelizedName, out listener!))
        {
            listenerName = camelizedName;
            return true;
        }

        listenerName = string.Empty;
        listener = null!;
        return false;
    }

    private async Task ObserveTaskCoreAsync(
        Task? task,
        string diagnosticInformation,
        bool rethrowIfUnhandled)
    {
        try
        {
            if (task is null)
            {
                throw new InvalidOperationException(
                    "An observed component operation returned a null task.");
            }

            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (Lifecycle.CancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RouteError(exception, diagnosticInformation, rethrowIfUnhandled);
        }
    }
}
