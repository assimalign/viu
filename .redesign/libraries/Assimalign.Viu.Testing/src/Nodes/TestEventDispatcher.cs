using System;
using System.Threading.Tasks;

namespace Assimalign.Viu.Testing;

/// <summary>Dispatches host events to delegates installed on an in-memory element.</summary>
/// <remarks>
/// Delegate shapes are dispatched explicitly, without reflection, to preserve trimming and AOT
/// safety. Specified by <c>[EXE-5]</c>, <c>[RND-IO-2]</c>, and <c>[CONF-3]</c>.
/// </remarks>
public static class TestEventDispatcher
{
    /// <summary>Triggers a synchronous listener when one is installed.</summary>
    /// <param name="element">The target element.</param>
    /// <param name="eventName">The event binding's local name.</param>
    /// <param name="payload">The optional listener payload.</param>
    /// <returns>Whether a listener was invoked.</returns>
    public static bool Trigger(TestElement element, string eventName, object? payload = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(eventName);
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
                case Func<Task>:
                case Func<object?, Task>:
                    throw new NotSupportedException(
                        $"Event listener for '{eventName}' is asynchronous; use TriggerAsync.");
                default:
                    throw UnsupportedListener(eventName, target);
            }
        }

        return true;
    }

    /// <summary>Triggers and awaits every installed synchronous or asynchronous listener.</summary>
    /// <param name="element">The target element.</param>
    /// <param name="eventName">The event binding's local name.</param>
    /// <param name="payload">The optional listener payload.</param>
    /// <returns>Whether a listener was invoked.</returns>
    public static async Task<bool> TriggerAsync(
        TestElement element,
        string eventName,
        object? payload = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(eventName);
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
                    await RequireTask(asynchronousAction(), eventName).ConfigureAwait(false);
                    break;
                case Func<object?, Task> asynchronousPayloadAction:
                    await RequireTask(
                        asynchronousPayloadAction(payload),
                        eventName).ConfigureAwait(false);
                    break;
                default:
                    throw UnsupportedListener(eventName, target);
            }
        }

        return true;
    }

    private static Task RequireTask(Task? task, string eventName) =>
        task ?? throw new InvalidOperationException(
            $"The asynchronous listener for '{eventName}' returned a null task.");

    private static NotSupportedException UnsupportedListener(
        string eventName,
        Delegate listener) =>
        new(
            $"Event listener for '{eventName}' has unsupported shape "
            + $"'{listener.GetType().Name}'.");
}
