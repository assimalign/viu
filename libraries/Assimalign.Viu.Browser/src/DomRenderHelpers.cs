using System;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Provides the browser-specific helper names emitted by the template compiler.
/// </summary>
/// <remarks>
/// The underscore-prefixed names deliberately mirror Vue's generated helper vocabulary. Directive
/// members are unresolved metadata only; redesigned Core remains responsible for resolving and
/// invoking a registered directive. Task-returning handler overloads preserve the returned
/// <see cref="Task"/> so browser dispatch can observe failures.
/// </remarks>
public static class DomRenderHelpers
{
    /// <summary>Gets the unresolved <c>v-show</c> directive marker.</summary>
    public static readonly IComponentDirectiveBinding _vShow =
        new ComponentDirectiveBinding("show");

    /// <summary>Gets the unresolved text <c>v-model</c> directive marker.</summary>
    public static readonly IComponentDirectiveBinding _vModelText =
        new ComponentDirectiveBinding("modelText");

    /// <summary>Gets the unresolved checkbox <c>v-model</c> directive marker.</summary>
    public static readonly IComponentDirectiveBinding _vModelCheckbox =
        new ComponentDirectiveBinding("modelCheckbox");

    /// <summary>Gets the unresolved radio <c>v-model</c> directive marker.</summary>
    public static readonly IComponentDirectiveBinding _vModelRadio =
        new ComponentDirectiveBinding("modelRadio");

    /// <summary>Gets the unresolved select <c>v-model</c> directive marker.</summary>
    public static readonly IComponentDirectiveBinding _vModelSelect =
        new ComponentDirectiveBinding("modelSelect");

    /// <summary>Gets the unresolved dynamic <c>v-model</c> directive marker.</summary>
    public static readonly IComponentDirectiveBinding _vModelDynamic =
        new ComponentDirectiveBinding("modelDynamic");

    /// <summary>
    /// Gets the named-template marker for Browser's <c>Transition</c> built-in.
    /// </summary>
    public static readonly object _Transition =
        RenderHelpers._resolveComponent("Transition");

    /// <summary>
    /// Gets the named-template marker for the browser <c>TransitionGroup</c> built-in.
    /// </summary>
    public static readonly object _TransitionGroup =
        RenderHelpers._resolveComponent("TransitionGroup");

    /// <summary>Applies event modifiers to a value-returning browser event handler.</summary>
    public static Action<BrowserEvent> _withModifiers(
        Func<BrowserEvent, object?> handler,
        params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithModifiers(
            browserEvent => _ = handler(browserEvent),
            modifiers);
    }

    /// <summary>Applies event modifiers to a synchronous browser event handler.</summary>
    public static Action<BrowserEvent> _withModifiers(
        Action<BrowserEvent> handler,
        params string[] modifiers)
    {
        return BrowserEvents.WithModifiers(handler, modifiers);
    }

    /// <summary>Applies event modifiers to a task-returning browser event handler.</summary>
    public static Func<BrowserEvent, Task> _withModifiers(
        Func<BrowserEvent, Task> handler,
        params string[] modifiers)
    {
        return BrowserEvents.WithModifiers(handler, modifiers);
    }

    /// <summary>Applies event modifiers to a value-returning parameterless handler.</summary>
    public static Action<BrowserEvent> _withModifiers(
        Func<object?> handler,
        params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithModifiers(
            _ =>
            {
                handler();
            },
            modifiers);
    }

    /// <summary>Applies event modifiers to a synchronous parameterless handler.</summary>
    public static Action<BrowserEvent> _withModifiers(
        Action handler,
        params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithModifiers(_ => handler(), modifiers);
    }

    /// <summary>Applies event modifiers to a task-returning parameterless handler.</summary>
    public static Func<BrowserEvent, Task> _withModifiers(
        Func<Task> handler,
        params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithModifiers(_ => handler(), modifiers);
    }

    /// <summary>Applies key guards to a value-returning browser event handler.</summary>
    public static Action<BrowserEvent> _withKeys(
        Func<BrowserEvent, object?> handler,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithKeys(
            browserEvent => _ = handler(browserEvent),
            keys);
    }

    /// <summary>Applies key guards to a synchronous browser event handler.</summary>
    public static Action<BrowserEvent> _withKeys(
        Action<BrowserEvent> handler,
        params string[] keys)
    {
        return BrowserEvents.WithKeys(handler, keys);
    }

    /// <summary>Applies key guards to a task-returning browser event handler.</summary>
    public static Func<BrowserEvent, Task> _withKeys(
        Func<BrowserEvent, Task> handler,
        params string[] keys)
    {
        return BrowserEvents.WithKeys(handler, keys);
    }

    /// <summary>Applies key guards to a value-returning parameterless handler.</summary>
    public static Action<BrowserEvent> _withKeys(
        Func<object?> handler,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithKeys(
            _ =>
            {
                handler();
            },
            keys);
    }

    /// <summary>Applies key guards to a synchronous parameterless handler.</summary>
    public static Action<BrowserEvent> _withKeys(
        Action handler,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithKeys(_ => handler(), keys);
    }

    /// <summary>Applies key guards to a task-returning parameterless handler.</summary>
    public static Func<BrowserEvent, Task> _withKeys(
        Func<Task> handler,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithKeys(_ => handler(), keys);
    }
}
