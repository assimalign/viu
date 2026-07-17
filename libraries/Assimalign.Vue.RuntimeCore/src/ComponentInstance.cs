using System;
using System.Collections.Generic;

using Assimalign.Vue.Reactivity;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// A mounted component's runtime state — the C# port of upstream's
/// <c>ComponentInternalInstance</c> (<c>packages/runtime-core/src/component.ts</c>,
/// https://vuejs.org/api/composition-api-setup.html). Carries the identity the rest of the
/// area depends on: <see cref="Uid"/> for scheduler ordering, <see cref="Parent"/>/<see cref="Root"/>
/// links, the provides table, per-kind lifecycle hook lists, the owning vnode and rendered
/// subtree back-pointers, and mount state. <see cref="Current"/> is the active-instance stack
/// (correct across nested setups, restored on exception).
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class ComponentInstance
{
    private static int _nextUid;
    private static readonly List<ComponentInstance> InstanceStack = [];
    private static readonly Dictionary<string, string> HandlerNameCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> CamelizeCache = new(StringComparer.Ordinal);

    private readonly List<Delegate>?[] _hooks = new List<Delegate>?[10];
    private readonly Dictionary<string, ComponentEmitDefinition>? _declaredEmits;
    private HashSet<string>? _emittedOnceEvents;

    internal ComponentInstance(IComponentDefinition definition, VirtualNode virtualNode, ComponentInstance? parent)
    {
        Uid = _nextUid++;
        Definition = definition;
        VirtualNode = virtualNode;
        Parent = parent;
        Root = parent?.Root ?? this;
        // Inherit the app context from the parent, or from the root vnode at the top of the tree
        // (upstream: appContext = parent ? parent.appContext : vnode.appContext). Carries app-level
        // provides, the component registry, and config to every instance ([V01.01.03.12]).
        AppContext = parent?.AppContext ?? virtualNode.AppContext;
        // Inherit the parent's provides table by reference (upstream: instance.provides =
        // parent.provides). DependencyInjection.Provide forks a layered copy on this instance's
        // first own provide; until then the reference is shared with every non-providing ancestor.
        Provides = parent?.Provides;
        Scope = new EffectScope(detached: true);
        Properties = new ComponentProperties(DisplayName);
        Attributes = new ComponentAttributes();
        if (definition.Properties is { Count: > 0 } declaredProperties)
        {
            // camelCase and kebab-case both resolve to the declaration (upstream parity).
            DeclaredProperties = new Dictionary<string, ComponentPropertyDefinition>(StringComparer.Ordinal);
            foreach (var property in declaredProperties)
            {
                DeclaredProperties[property.Name] = property;
                if (property.KebabName is not null)
                {
                    DeclaredProperties[property.KebabName] = property;
                }
            }
        }
        if (definition.Emits is { Count: > 0 } emits)
        {
            _declaredEmits = new Dictionary<string, ComponentEmitDefinition>(emits.Count, StringComparer.Ordinal);
            foreach (var emit in emits)
            {
                _declaredEmits[emit.Name] = emit;
            }
        }
    }

    /// <summary>
    /// The application context this instance belongs to (upstream: <c>instance.appContext</c>),
    /// inherited from the parent or the root vnode. Carries app-level provides (the final inject
    /// fallback), the component registry, and <see cref="ApplicationConfiguration"/>. Null for a
    /// tree rendered without an application ([V01.01.03.12]).
    /// </summary>
    internal ApplicationContext? AppContext { get; }

    /// <summary>Declared-prop lookup by camelCase AND kebab-case name; null when none declared.</summary>
    internal Dictionary<string, ComponentPropertyDefinition>? DeclaredProperties { get; }

    /// <summary>The declared prop names the parent explicitly provided in the last resolve pass.</summary>
    internal HashSet<string>? LastProvidedNames { get; set; }

    /// <summary>
    /// The instance whose <c>Setup</c>, render, or lifecycle hook is executing — upstream's
    /// <c>getCurrentInstance()</c>. Null outside those windows.
    /// </summary>
    public static ComponentInstance? Current
        => InstanceStack.Count > 0 ? InstanceStack[^1] : null;

    /// <summary>The creation-ordered uid; parents have lower uids, which orders scheduler jobs.</summary>
    public int Uid { get; }

    /// <summary>The component definition this instance runs.</summary>
    public IComponentDefinition Definition { get; }

    /// <summary>The parent instance, or null at the root.</summary>
    public ComponentInstance? Parent { get; }

    /// <summary>The root instance of this component tree.</summary>
    public ComponentInstance Root { get; }

    /// <summary>The effect scope every setup-created effect/computed registers with.</summary>
    public EffectScope Scope { get; }

    /// <summary>The shallow-reactive props (upstream: <c>instance.props</c>).</summary>
    public ComponentProperties Properties { get; }

    /// <summary>The live fallthrough attributes (upstream: <c>instance.attrs</c>).</summary>
    public ComponentAttributes Attributes { get; }

    /// <summary>
    /// The instance's slots object (upstream: <c>instance.slots</c>), rendered through
    /// <see cref="VirtualNodeFactory.RenderSlot"/>. Installed at mount and refreshed on a
    /// slot-affecting parent update; null when the parent passed no slot content.
    /// </summary>
    public ComponentSlots? Slots { get; internal set; }

    /// <summary>The state surfaced to parent refs via <c>Expose</c>, or null.</summary>
    public object? Exposed { get; private set; }

    /// <summary>The vnode this instance is mounted for (upstream: <c>instance.vnode</c>).</summary>
    public VirtualNode VirtualNode { get; internal set; }

    /// <summary>The rendered subtree (upstream: <c>instance.subTree</c>).</summary>
    public VirtualNode? Subtree { get; internal set; }

    /// <summary>Whether the first mount completed.</summary>
    public bool IsMounted { get; internal set; }

    /// <summary>Whether the instance was torn down.</summary>
    public bool IsUnmounted { get; internal set; }

    /// <summary>
    /// The dependency-injection provides table (upstream: <c>instance.provides</c>), consumed by
    /// <see cref="DependencyInjection"/>. Inherited from the parent by reference and forked into a
    /// layered copy on this instance's first own provide (copy-on-first-provide); null when neither
    /// this instance nor any ancestor has provided anything.
    /// </summary>
    internal Dictionary<object, object?>? Provides { get; set; }

    internal Func<VirtualNode?>? RenderFunction { get; set; }

    internal ReactiveEffect? Effect { get; set; }

    internal SchedulerJob? UpdateJob { get; set; }

    /// <summary>A pending vnode from a parent-driven update (upstream: <c>instance.next</c>).</summary>
    internal VirtualNode? NextVirtualNode { get; set; }

    internal string DisplayName => Definition.Name ?? Definition.GetType().Name;

    /// <summary>
    /// Upstream <c>toggleRecurse</c>: self-triggering is disabled while props resolve and
    /// before-hooks run (a props write during the effect's own run must not requeue it), and
    /// re-enabled for render/patch.
    /// </summary>
    internal void ToggleRecurse(bool allowed)
    {
        Effect!.AllowRecurse = allowed;
        UpdateJob!.AllowRecurse = allowed;
    }

    internal void SetExposed(object? exposed) => Exposed = exposed;

    // --- current-instance stack ------------------------------------------------------------

    internal void PushCurrent() => InstanceStack.Add(this);

    internal void PopCurrent()
    {
        if (InstanceStack.Count > 0 && ReferenceEquals(InstanceStack[^1], this))
        {
            InstanceStack.RemoveAt(InstanceStack.Count - 1);
        }
    }

    // --- lifecycle hooks ---------------------------------------------------------------------

    internal void RegisterHook(LifecycleHookKind kind, Delegate hook)
        => (_hooks[(int)kind] ??= []).Add(hook);

    internal bool HasHooks(LifecycleHookKind kind) => _hooks[(int)kind] is { Count: > 0 };

    /// <summary>Runs the instance's hooks of one kind, in registration order, with this instance current.</summary>
    internal void InvokeHooks(LifecycleHookKind kind)
    {
        var hooks = _hooks[(int)kind];
        if (hooks is null || IsUnmounted && kind is not LifecycleHookKind.Unmounted)
        {
            return;
        }
        PushCurrent();
        try
        {
            foreach (var hook in hooks)
            {
                try
                {
                    ((Action)hook)();
                }
                catch (Exception exception)
                {
                    ComponentErrorHandling.Handle(exception, this, $"{kind} hook");
                }
            }
        }
        finally
        {
            PopCurrent();
        }
    }

    internal IReadOnlyList<Delegate>? GetHooks(LifecycleHookKind kind) => _hooks[(int)kind];

    // --- emits -------------------------------------------------------------------------------

    /// <summary>
    /// Dispatches a component event to its handler prop (upstream: <c>emit</c> in
    /// <c>componentEmits.ts</c>): validates against the declaration, honors casing rules and
    /// <c>Once</c> handlers, and forms the <c>update:modelValue</c> runtime contract.
    /// </summary>
    /// <param name="eventName">The event name as emitted.</param>
    /// <param name="arguments">The event payload.</param>
    internal void EmitEvent(string eventName, object?[] arguments)
    {
        // Test-utilities observation: capture the emit (declared or not, handled or not) before
        // dispatch, so a Testing wrapper records every event in order ([V01.01.11.02]). Inert in
        // production (observer null).
        AppContext?.EmitObserver?.Invoke(this, eventName, arguments);
        if (_declaredEmits is not null)
        {
            if (!_declaredEmits.TryGetValue(eventName, out var declaration))
            {
                RuntimeWarnings.Warn(
                    $"Component <{DisplayName}> emitted event \"{eventName}\" but it is not declared in the "
                    + "Emits option.");
            }
            else if (declaration.Validator is not null && !declaration.Validator(arguments))
            {
                RuntimeWarnings.Warn($"Invalid event arguments: event validation failed for event \"{eventName}\".");
            }
        }
        var properties = VirtualNode.Properties;
        if (properties is null)
        {
            return;
        }
        var handlerName = ToHandlerPropertyName(eventName);
        if (!properties.TryGetValue(handlerName, out var handler) || handler is null)
        {
            // Kebab-case emit matches a camelCase handler (upstream parity).
            var camelizedHandlerName = ToHandlerPropertyName(Camelize(eventName));
            properties.TryGetValue(camelizedHandlerName, out handler);
            handlerName = camelizedHandlerName;
        }
        if (handler is Delegate liveHandler)
        {
            InvokeEmitHandler(liveHandler, arguments, eventName);
        }
        // Once semantics: fires exactly once per instance across re-renders (upstream
        // instance.emitted tracking).
        var onceName = handlerName + "Once";
        if (properties.TryGetValue(onceName, out var onceHandler) && onceHandler is Delegate onceDelegate)
        {
            _emittedOnceEvents ??= new HashSet<string>(StringComparer.Ordinal);
            if (_emittedOnceEvents.Add(onceName))
            {
                InvokeEmitHandler(onceDelegate, arguments, eventName);
            }
        }
    }

    /// <summary>Whether <paramref name="propertyName"/> is a declared emit's handler prop (attrs exclusion).</summary>
    internal bool IsDeclaredEmitHandlerName(string propertyName)
    {
        if (_declaredEmits is null || !VirtualNodeFactory.IsEventListenerName(propertyName))
        {
            return false;
        }
        foreach (var name in _declaredEmits.Keys)
        {
            var handlerName = ToHandlerPropertyName(name);
            if (string.Equals(propertyName, handlerName, StringComparison.Ordinal)
                || (propertyName.EndsWith("Once", StringComparison.Ordinal)
                    && string.Equals(propertyName[..^4], handlerName, StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
    }

    private void InvokeEmitHandler(Delegate handler, object?[] arguments, string eventName)
    {
        try
        {
            switch (handler)
            {
                case Action action:
                    action();
                    break;
                case Action<object?> single:
                    single(arguments.Length > 0 ? arguments[0] : null);
                    break;
                case Action<object?[]> spread:
                    spread(arguments);
                    break;
                default:
                    RuntimeWarnings.Warn(
                        $"Handler for event \"{eventName}\" on <{DisplayName}> is a {handler.GetType().Name}; "
                        + "supported shapes are Action, Action<object?>, and Action<object?[]>.");
                    break;
            }
        }
        catch (Exception exception)
        {
            ComponentErrorHandling.Handle(exception, this, $"event handler for \"{eventName}\"");
        }
    }

    /// <summary>Cached <c>toHandlerKey</c>: <c>"change"</c> → <c>"onChange"</c>, <c>"update:modelValue"</c> → <c>"onUpdate:modelValue"</c>.</summary>
    internal static string ToHandlerPropertyName(string eventName)
    {
        if (HandlerNameCache.TryGetValue(eventName, out var cached))
        {
            return cached;
        }
        var handlerName = eventName.Length == 0
            ? "on"
            : $"on{char.ToUpperInvariant(eventName[0])}{eventName[1..]}";
        HandlerNameCache[eventName] = handlerName;
        return handlerName;
    }

    /// <summary>Cached <c>camelize</c>: <c>"my-event"</c> → <c>"myEvent"</c>.</summary>
    internal static string Camelize(string name)
    {
        if (CamelizeCache.TryGetValue(name, out var cached))
        {
            return cached;
        }
        var camelized = name;
        var hyphenIndex = name.IndexOf('-', StringComparison.Ordinal);
        if (hyphenIndex >= 0)
        {
            var builder = new System.Text.StringBuilder(name.Length);
            var upperNext = false;
            foreach (var character in name)
            {
                if (character == '-')
                {
                    upperNext = true;
                    continue;
                }
                builder.Append(upperNext ? char.ToUpperInvariant(character) : character);
                upperNext = false;
            }
            camelized = builder.ToString();
        }
        CamelizeCache[name] = camelized;
        return camelized;
    }
}
