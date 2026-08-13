using System;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Dispatches host events to delegates installed on an in-memory element.</summary>
/// <remarks>
/// Delegate shapes are dispatched explicitly, without reflection, to preserve trimming and AOT
/// safety. Portable event handlers receive the exact <see cref="IElementEvent"/> supplied by the
/// test. Specified by <c>[EXE-5]</c>, <c>[RND-IO-2]</c>, <c>[CONF-3]</c>, and
/// <c>[V01.01.11.06]</c>.
/// </remarks>
public static class TestEventDispatcher
{
    /// <summary>Triggers a synchronous listener without an event payload.</summary>
    /// <param name="element">The target element.</param>
    /// <param name="eventName">The event binding's normalized local name.</param>
    /// <returns>Whether a listener was invoked.</returns>
    public static bool Trigger(TestElement element, string eventName) =>
        Trigger(element, eventName, payload: null);

    /// <summary>Triggers a synchronous listener with an object payload.</summary>
    /// <param name="element">The target element.</param>
    /// <param name="eventName">The event binding's normalized local name.</param>
    /// <param name="payload">The legacy object payload, which may be null.</param>
    /// <returns>Whether a listener was invoked.</returns>
    public static bool Trigger(TestElement element, string eventName, object? payload)
    {
        Delegate? listener = TakeListener(element, eventName);
        if (listener is null)
        {
            return false;
        }

        foreach (Delegate target in listener.GetInvocationList())
        {
            switch (target)
            {
                case Action action:
                    action();
                    break;
                case Action<object?> payloadAction:
                    payloadAction(payload);
                    break;
                case Action<IElementEvent> elementEventAction
                    when payload is IElementEvent elementEvent:
                    elementEventAction(elementEvent);
                    break;
                case Func<Task>:
                case Func<object?, Task>:
                case Func<IElementEvent, Task>:
                    throw AsynchronousListener(eventName);
                default:
                    throw UnsupportedListener(eventName, target);
            }
        }

        return true;
    }

    /// <summary>Triggers a synchronous typed portable-event listener with exact payload identity.</summary>
    /// <typeparam name="TEvent">The concrete portable event type.</typeparam>
    /// <param name="element">The target element.</param>
    /// <param name="eventName">The event binding's normalized local name.</param>
    /// <param name="payload">The exact payload delivered to the listener.</param>
    /// <returns>Whether a listener was invoked.</returns>
    public static bool Trigger<TEvent>(
        TestElement element,
        string eventName,
        TEvent payload)
        where TEvent : IElementEvent
    {
        ArgumentNullException.ThrowIfNull(payload);
        Delegate? listener = TakeListener(element, eventName);
        if (listener is null)
        {
            return false;
        }

        InvokeTyped(listener, eventName, payload, payload);
        return true;
    }

    /// <summary>Triggers and awaits every listener without an event payload.</summary>
    /// <param name="element">The target element.</param>
    /// <param name="eventName">The event binding's normalized local name.</param>
    /// <returns>Whether a listener was invoked.</returns>
    public static Task<bool> TriggerAsync(TestElement element, string eventName) =>
        TriggerAsync(element, eventName, payload: null);

    /// <summary>Triggers and awaits every listener with an object payload.</summary>
    /// <param name="element">The target element.</param>
    /// <param name="eventName">The event binding's normalized local name.</param>
    /// <param name="payload">The legacy object payload, which may be null.</param>
    /// <returns>Whether a listener was invoked.</returns>
    public static async Task<bool> TriggerAsync(
        TestElement element,
        string eventName,
        object? payload)
    {
        Delegate? listener = TakeListener(element, eventName);
        if (listener is null)
        {
            return false;
        }

        foreach (Delegate target in listener.GetInvocationList())
        {
            switch (target)
            {
                case Action action:
                    action();
                    break;
                case Action<object?> payloadAction:
                    payloadAction(payload);
                    break;
                case Action<IElementEvent> elementEventAction
                    when payload is IElementEvent elementEvent:
                    elementEventAction(elementEvent);
                    break;
                case Func<Task> asynchronousAction:
                    await RequireTask(asynchronousAction(), eventName);
                    break;
                case Func<object?, Task> asynchronousPayloadAction:
                    await RequireTask(asynchronousPayloadAction(payload), eventName);
                    break;
                case Func<IElementEvent, Task> asynchronousElementEventAction
                    when payload is IElementEvent asynchronousElementEvent:
                    await RequireTask(
                        asynchronousElementEventAction(asynchronousElementEvent),
                        eventName);
                    break;
                default:
                    throw UnsupportedListener(eventName, target);
            }
        }

        return true;
    }

    /// <summary>Triggers and awaits a typed portable-event listener with exact payload identity.</summary>
    /// <typeparam name="TEvent">The concrete portable event type.</typeparam>
    /// <param name="element">The target element.</param>
    /// <param name="eventName">The event binding's normalized local name.</param>
    /// <param name="payload">The exact payload delivered to the listener.</param>
    /// <returns>Whether a listener was invoked.</returns>
    public static Task<bool> TriggerAsync<TEvent>(
        TestElement element,
        string eventName,
        TEvent payload)
        where TEvent : IElementEvent
    {
        ArgumentNullException.ThrowIfNull(payload);
        return TriggerTypedAsync(element, eventName, payload, payload);
    }

    internal static Task<bool> TriggerAsync<TEvent>(
        TestElement element,
        string eventName,
        TEvent payload,
        object? objectPayload)
        where TEvent : IElementEvent
    {
        ArgumentNullException.ThrowIfNull(payload);
        return TriggerTypedAsync(element, eventName, payload, objectPayload);
    }

    private static async Task<bool> TriggerTypedAsync<TEvent>(
        TestElement element,
        string eventName,
        TEvent payload,
        object? objectPayload)
        where TEvent : IElementEvent
    {
        Delegate? listener = TakeListener(element, eventName);
        if (listener is null)
        {
            return false;
        }

        foreach (Delegate target in listener.GetInvocationList())
        {
            switch (target)
            {
                // Action<T> is contravariant. Object and portable-interface cases must precede
                // the concrete TEvent case or they would be consumed by it with the wrong payload.
                case Action<object?> objectAction:
                    objectAction(objectPayload);
                    break;
                case Action<IElementEvent> elementEventAction:
                    elementEventAction(payload);
                    break;
                case Action<TEvent> typedAction:
                    typedAction(payload);
                    break;
                case Action action:
                    action();
                    break;
                case Func<object?, Task> asynchronousObjectAction:
                    await RequireTask(asynchronousObjectAction(objectPayload), eventName);
                    break;
                case Func<IElementEvent, Task> asynchronousElementEventAction:
                    await RequireTask(asynchronousElementEventAction(payload), eventName);
                    break;
                case Func<TEvent, Task> asynchronousTypedAction:
                    await RequireTask(asynchronousTypedAction(payload), eventName);
                    break;
                case Func<Task> asynchronousAction:
                    await RequireTask(asynchronousAction(), eventName);
                    break;
                default:
                    throw UnsupportedListener(eventName, target);
            }
        }

        return true;
    }

    private static void InvokeTyped<TEvent>(
        Delegate listener,
        string eventName,
        TEvent payload,
        object? objectPayload)
        where TEvent : IElementEvent
    {
        foreach (Delegate target in listener.GetInvocationList())
        {
            switch (target)
            {
                case Action<object?> objectAction:
                    objectAction(objectPayload);
                    break;
                case Action<IElementEvent> elementEventAction:
                    elementEventAction(payload);
                    break;
                case Action<TEvent> typedAction:
                    typedAction(payload);
                    break;
                case Action action:
                    action();
                    break;
                case Func<object?, Task>:
                case Func<IElementEvent, Task>:
                case Func<TEvent, Task>:
                case Func<Task>:
                    throw AsynchronousListener(eventName);
                default:
                    throw UnsupportedListener(eventName, target);
            }
        }
    }

    private static Delegate? TakeListener(TestElement element, string eventName)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        return element.TryTakeEventListener(eventName, out Delegate? listener)
            ? listener
            : null;
    }

    private static Task RequireTask(Task? task, string eventName) =>
        task ?? throw new InvalidOperationException(
            $"The asynchronous listener for '{eventName}' returned a null task.");

    private static NotSupportedException AsynchronousListener(string eventName) =>
        new($"Event listener for '{eventName}' is asynchronous; use TriggerAsync.");

    private static NotSupportedException UnsupportedListener(
        string eventName,
        Delegate listener) =>
        new(
            $"Event listener for '{eventName}' has unsupported shape "
            + $"'{listener.GetType().Name}'. Use Action, Action<object?>, "
            + $"Action<{nameof(IElementEvent)}>, or their task-returning forms.");
}
