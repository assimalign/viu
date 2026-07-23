using System;
using System.Threading.Tasks;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Dispatches named events to the listeners an element registered through <c>patchProp</c> —
/// the C# port of <c>triggerEvent</c> in <c>@vue/runtime-test</c>
/// (<c>packages/runtime-test/src/triggerEvent.ts</c>). Listeners are invoked synchronously on
/// the test node; multicast delegates (merged handlers) invoke every target in order.
/// </summary>
public static class TestEventDispatcher
{
    /// <summary>Triggers <paramref name="eventName"/> on <paramref name="element"/>.</summary>
    /// <param name="element">The element carrying the listener.</param>
    /// <param name="eventName">The lower-case event name (an <c>onClick</c> prop listens to <c>"click"</c>).</param>
    /// <param name="payload">The event payload passed to payload-accepting listeners.</param>
    /// <returns>Whether a listener was registered and invoked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> or <paramref name="eventName"/> is null.</exception>
    /// <exception cref="NotSupportedException">The listener is not a supported synchronous shape.</exception>
    public static bool Trigger(TestElement element, string eventName, object? payload = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(eventName);
        if (!element.EventListeners.TryGetValue(eventName, out var listener))
        {
            return false;
        }
        // No DynamicInvoke — reflection-based invocation is forbidden (AOT/trimming rules).
        foreach (var target in listener.GetInvocationList())
        {
            switch (target)
            {
                case Action action:
                    action();
                    break;
                case Action<object?> payloadAction:
                    payloadAction(payload);
                    break;
                case Func<Task>:
                case Func<object?, Task>:
                    throw new NotSupportedException(
                        $"Event listener for '{eventName}' returns a task. Use TriggerAsync.");
                default:
                    throw new NotSupportedException(
                        $"Event listener for '{eventName}' is a {target.GetType().Name}; the test dispatcher "
                        + "invokes Action or Action<object?> listeners only.");
            }
        }
        return true;
    }

    /// <summary>
    /// Triggers an event and awaits synchronous or task-returning listener delegates in registration
    /// order.
    /// </summary>
    /// <param name="element">The element carrying the listener.</param>
    /// <param name="eventName">The lower-case event name.</param>
    /// <param name="payload">The optional event payload.</param>
    /// <returns>Whether a listener was registered and invoked.</returns>
    public static async Task<bool> TriggerAsync(
        TestElement element,
        string eventName,
        object? payload = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(eventName);
        if (!element.EventListeners.TryGetValue(eventName, out Delegate? listener))
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
                case Func<Task> asynchronousAction:
                    await RequireTask(
                        asynchronousAction(),
                        eventName).ConfigureAwait(false);
                    break;
                case Func<object?, Task> asynchronousPayloadAction:
                    await RequireTask(
                        asynchronousPayloadAction(payload),
                        eventName).ConfigureAwait(false);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Event listener for '{eventName}' is a {target.GetType().Name}; the test "
                        + "dispatcher invokes Action, Action<object?>, Func<Task>, or "
                        + "Func<object?, Task> listeners only.");
            }
        }

        return true;
    }

    private static Task RequireTask(Task? task, string eventName)
    {
        return task
            ?? throw new InvalidOperationException(
                $"The asynchronous listener for '{eventName}' returned a null task.");
    }
}
