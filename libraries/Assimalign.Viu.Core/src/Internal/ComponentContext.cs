using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Shared;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>The live setup context owned by one mounted template.</summary>
internal sealed class ComponentContext :
    IComponentContext,
    IComponentWarningContext,
    IStateStoreContext
{
    private static readonly IReadOnlyDictionary<string, ComponentSlot> _emptySlots =
        new ReadOnlyDictionary<string, ComponentSlot>(
            new Dictionary<string, ComponentSlot>(StringComparer.Ordinal));

    private static ComponentContext? _current;

    private readonly IComponentTemplate _template;
    private readonly Dictionary<string, IComponentParameter> _parameters =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, IComponentParameter> _parameterAliases =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _defaultValues =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, IComponentEvent> _events =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, IComponentEvent> _eventAliases =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _emittedOnceListeners =
        new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, ComponentEventListener>? _listeners;

    internal ComponentContext(
        IApplicationContext application,
        IComponentTemplate template,
        ITemplateComponent request,
        EffectScope scope,
        ComponentContext? parent,
        int identifier)
    {
        Application = application;
        _template = template;
        Scope = scope;
        Parent = parent;
        Identifier = identifier;
        WatchScheduler = new ApplicationWatchScheduler(identifier);
        Lifecycle = new ComponentLifecycle();
        SuspenseBoundary =
            parent?.SuspenseBoundary
            ?? SuspenseBoundaryContext.Current;

        if (template.Parameters is not null)
        {
            foreach (IComponentParameter parameter in template.Parameters)
            {
                ArgumentNullException.ThrowIfNull(parameter);
                ArgumentException.ThrowIfNullOrEmpty(parameter.Name);
                _parameters.Add(parameter.Name, parameter);
                AddAlias(_parameterAliases, parameter.Name, parameter, "parameter");
                AddAlias(
                    _parameterAliases,
                    Camelize(parameter.Name),
                    parameter,
                    "parameter");
                AddAlias(
                    _parameterAliases,
                    StyleAndClassNormalization.Hyphenate(parameter.Name),
                    parameter,
                    "parameter");
            }
        }

        if (template.Events is not null)
        {
            foreach (IComponentEvent componentEvent in template.Events)
            {
                ArgumentNullException.ThrowIfNull(componentEvent);
                ArgumentException.ThrowIfNullOrEmpty(componentEvent.Name);
                _events.Add(componentEvent.Name, componentEvent);
                AddAlias(_eventAliases, componentEvent.Name, componentEvent, "event");
                AddAlias(
                    _eventAliases,
                    Camelize(componentEvent.Name),
                    componentEvent,
                    "event");
            }
        }

        Update(request, isInitial: true);
    }

    /// <summary>Gets the context active during setup, render, or lifecycle invocation.</summary>
    internal static ComponentContext? Current => _current;

    internal IApplicationContext Application { get; }

    internal ComponentContext? Parent { get; }

    internal int Identifier { get; }

    internal string? ScopeIdentifier => _template.ScopeIdentifier;

    internal EffectScope Scope { get; }

    internal ApplicationWatchScheduler WatchScheduler { get; }

    internal ISuspenseBoundary? SuspenseBoundary { get; set; }

    internal bool IsUnmounted { get; set; }

    internal object? Exposed { get; private set; }

    internal bool HasExposed { get; private set; }

    internal Func<IReadOnlyList<object>>? RootElementResolver { get; set; }

    internal Func<IReadOnlyList<KeyedComponentHostElementSnapshot>>?
        KeyedChildElementResolver { get; set; }

    internal Action? HostCommitScheduler { get; set; }

    internal ITemplateComponent Request { get; private set; } = null!;

    internal IReadOnlyList<IComponentDirectiveBinding> RootDirectives { get; private set; } =
        Array.Empty<IComponentDirectiveBinding>();

    /// <inheritdoc/>
    public IComponentArguments Arguments { get; private set; } = new ComponentArguments();

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, ComponentSlot> Slots { get; private set; } = _emptySlots;

    /// <inheritdoc/>
    public IComponentAttributeCollection Attributes { get; private set; } =
        new ComponentAttributes();

    /// <inheritdoc/>
    public IComponentFactory Components => Application.Components;

    /// <inheritdoc/>
    public IServiceProvider Services => Application.Services;

    /// <inheritdoc/>
    public IStateStoreRegistry? State => Application.State;

    /// <inheritdoc/>
    public ComponentLifecycle Lifecycle { get; }

    IComponentLifecycle IComponentContext.Lifecycle => Lifecycle;

    /// <inheritdoc/>
    public void Emit(string eventName, params object?[] arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        arguments ??= [null];
        IReadOnlyList<object?> eventArguments = arguments;
        if (Application is ApplicationContext applicationContext)
        {
            applicationContext.EventObserver?.Invoke(this, eventName, eventArguments);
        }

        IComponentEvent? declaration = ResolveEvent(eventName);
        if (_events.Count > 0 && declaration is null)
        {
            Application.WarnHandler?.Invoke(
                $"Component \"{_template.Name ?? _template.GetType().Name}\" emitted undeclared "
                + $"event \"{eventName}\".");
        }
        else if (declaration?.Validator is not null
            && !Run(() => declaration.Validator(eventArguments)))
        {
            Application.WarnHandler?.Invoke(
                $"Invalid arguments were emitted for component event \"{eventName}\".");
        }

        if (_listeners is null)
        {
            return;
        }

        if (TryGetListener(
            eventName,
            isOnceName: false,
            out string? listenerName,
            out ComponentEventListener? listener))
        {
            InvokeListener(
                listenerName,
                listener,
                eventName,
                eventArguments,
                listener.IsOnce);
        }

        if (TryGetListener(eventName, isOnceName: true, out listenerName, out listener))
        {
            InvokeListener(
                listenerName,
                listener,
                eventName,
                eventArguments,
                isOnce: true);
        }
    }

    /// <inheritdoc/>
    public void Expose(object? value)
    {
        HasExposed = true;
        Exposed = value;
    }

    void IComponentWarningContext.Warn(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        Application.WarnHandler?.Invoke(message);
    }

    internal void Update(ITemplateComponent request, bool isInitial = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;

        Dictionary<string, object?> arguments = new(StringComparer.Ordinal);
        List<IComponentAttribute> attributes = [];
        foreach (KeyValuePair<string, object?> supplied in request.Arguments)
        {
            if (IsComponentNodeLifecycleName(supplied.Key))
            {
                continue;
            }

            if (_parameterAliases.TryGetValue(
                supplied.Key,
                out IComponentParameter? parameter))
            {
                arguments[parameter.Name] = supplied.Value;
                ValidateParameter(parameter, supplied.Value);
            }
            else if (IsDeclaredEventListener(supplied.Key))
            {
                continue;
            }
            else
            {
                attributes.Add(new ComponentAttribute(supplied.Key, supplied.Value));
            }
        }

        foreach (KeyValuePair<string, IComponentParameter> declared in _parameters)
        {
            if (arguments.ContainsKey(declared.Key))
            {
                continue;
            }

            if (declared.Value.DefaultFactory is not null)
            {
                if (!_defaultValues.TryGetValue(declared.Key, out object? defaultValue))
                {
                    defaultValue = declared.Value.DefaultFactory();
                    _defaultValues.Add(declared.Key, defaultValue);
                }

                arguments.Add(declared.Key, defaultValue);
                ValidateParameter(declared.Value, defaultValue);
            }
            else if (declared.Value.IsRequired && isInitial)
            {
                Application.WarnHandler?.Invoke(
                    $"Required component argument \"{declared.Key}\" was not supplied.");
            }
        }

        Arguments = new ComponentArguments(arguments);
        Attributes = new ComponentAttributes(attributes);
        Slots = request.Slots ?? _emptySlots;
        _listeners = request.Listeners;
        RootDirectives = request.Directives;
    }

    internal void UpdateRequest(ITemplateComponent request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
        _listeners = request.Listeners;
    }

    private string ComponentName => _template.Name ?? _template.GetType().Name;

    private IComponentEvent? ResolveEvent(string eventName)
    {
        if (_eventAliases.TryGetValue(eventName, out IComponentEvent? declaration))
        {
            return declaration;
        }

        _eventAliases.TryGetValue(Camelize(eventName), out declaration);
        return declaration;
    }

    internal bool IsDeclaredEventListener(string argumentName)
    {
        if (!IsEventListenerName(argumentName))
        {
            return false;
        }

        string eventName = ToEventName(argumentName);
        if (ResolveEvent(eventName) is not null)
        {
            return true;
        }

        return eventName.EndsWith("Once", StringComparison.Ordinal)
            && ResolveEvent(eventName[..^4]) is not null;
    }

    private bool TryGetListener(
        string eventName,
        bool isOnceName,
        out string listenerName,
        out ComponentEventListener listener)
    {
        string suffix = isOnceName ? "Once" : string.Empty;
        string exactName = eventName + suffix;
        if (_listeners!.TryGetValue(exactName, out listener!))
        {
            listenerName = exactName;
            return true;
        }

        string camelizedName = Camelize(eventName) + suffix;
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

    private void InvokeListener(
        string listenerName,
        ComponentEventListener listener,
        string eventName,
        IReadOnlyList<object?> arguments,
        bool isOnce)
    {
        if (isOnce && !_emittedOnceListeners.Add(listenerName))
        {
            return;
        }

        string diagnosticInformation = $"component event listener \"{eventName}\"";
        try
        {
            if (listener.ArgumentsHandler is not null)
            {
                Run(() => listener.ArgumentsHandler(arguments));
                return;
            }

            if (listener.AsynchronousArgumentsHandler is not null)
            {
                ObserveAllTasks(
                    listener.AsynchronousArgumentsHandler,
                    arguments,
                    diagnosticInformation);
                return;
            }

            object? value = arguments.Count > 0 ? arguments[0] : null;
            if (listener.Handler is not null)
            {
                Run(() => listener.Handler(value));
                return;
            }

            ObserveAllTasks(
                listener.AsynchronousHandler!,
                value,
                diagnosticInformation);
        }
        catch (Exception exception)
        {
            ComponentErrorHandling.Handle(
                exception,
                this,
                diagnosticInformation);
        }
    }

    private void ObserveAllTasks(
        AsynchronousComponentEventHandler handler,
        object? value,
        string diagnosticInformation)
    {
        foreach (Delegate invocation in handler.GetInvocationList())
        {
            Task task = Run(
                () => ((AsynchronousComponentEventHandler)invocation)(value));
            ObserveTask(task, diagnosticInformation);
        }
    }

    private void ObserveAllTasks(
        AsynchronousComponentEventArgumentsHandler handler,
        IReadOnlyList<object?> arguments,
        string diagnosticInformation)
    {
        foreach (Delegate invocation in handler.GetInvocationList())
        {
            Task task = Run(
                () => ((AsynchronousComponentEventArgumentsHandler)invocation)(arguments));
            ObserveTask(task, diagnosticInformation);
        }
    }

    private void ValidateParameter(
        IComponentParameter parameter,
        object? value)
    {
        if (parameter.Validator is not null
            && !Run(() => parameter.Validator(value)))
        {
            Application.WarnHandler?.Invoke(
                $"Invalid value was supplied for component argument \"{parameter.Name}\" "
                + $"on component \"{ComponentName}\".");
        }
    }

    private static bool IsEventListenerName(string name)
    {
        return name.Length > 2
            && name[0] == 'o'
            && name[1] == 'n'
            && char.IsAsciiLetterUpper(name[2]);
    }

    private static bool IsComponentNodeLifecycleName(string name)
    {
        return name.StartsWith("onVnode", StringComparison.Ordinal);
    }

    private static string ToEventName(string listenerName)
    {
        string name = listenerName[2..];
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string Camelize(string name)
    {
        int firstHyphen = name.IndexOf('-', StringComparison.Ordinal);
        if (firstHyphen < 0)
        {
            return name;
        }

        StringBuilder result = new(name.Length);
        bool uppercaseNext = false;
        foreach (char character in name)
        {
            if (character == '-')
            {
                uppercaseNext = true;
                continue;
            }

            result.Append(
                uppercaseNext
                    ? char.ToUpperInvariant(character)
                    : character);
            uppercaseNext = false;
        }

        return result.ToString();
    }

    private static void AddAlias<T>(
        Dictionary<string, T> aliases,
        string alias,
        T declaration,
        string declarationKind)
        where T : class
    {
        if (aliases.TryGetValue(alias, out T? existing))
        {
            if (!ReferenceEquals(existing, declaration))
            {
                throw new InvalidOperationException(
                    $"Component {declarationKind} alias \"{alias}\" is declared more than once.");
            }

            return;
        }

        aliases.Add(alias, declaration);
    }

    internal void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ComponentContext? previous = _current;
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
        ComponentContext? previous = _current;
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

    internal void ObserveTask(Task? task, string diagnosticInformation)
    {
        if (task is null)
        {
            ComponentErrorHandling.Handle(
                new InvalidOperationException("An asynchronous component callback returned a null task."),
                this,
                diagnosticInformation);
            return;
        }

        _ = ObserveTaskCoreAsync(task, diagnosticInformation);
    }

    private async Task ObserveTaskCoreAsync(Task task, string diagnosticInformation)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (Lifecycle.CancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ComponentErrorHandling.HandleObservedTaskError(
                exception,
                this,
                diagnosticInformation);
        }
    }
}
