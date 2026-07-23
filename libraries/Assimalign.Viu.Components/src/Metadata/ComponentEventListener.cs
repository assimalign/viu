using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Carries a single-payload or all-arguments parent listener for one component event.
/// </summary>
/// <remarks>
/// Components only transports the delegates. Core invokes the selected delegate and owns task
/// observation, error routing, and component-lifetime cancellation policy.
/// </remarks>
public sealed class ComponentEventListener
{
    /// <summary>Creates a synchronous component-event listener.</summary>
    /// <param name="handler">The listener delegate.</param>
    /// <param name="isOnce">Whether Core invokes the listener at most once per mount.</param>
    public ComponentEventListener(
        ComponentEventHandler handler,
        bool isOnce = false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Handler = handler;
        IsOnce = isOnce;
    }

    /// <summary>Creates an asynchronous component-event listener.</summary>
    /// <param name="handler">The task-returning listener delegate.</param>
    /// <param name="isOnce">Whether Core invokes the listener at most once per mount.</param>
    public ComponentEventListener(
        AsynchronousComponentEventHandler handler,
        bool isOnce = false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        AsynchronousHandler = handler;
        IsOnce = isOnce;
    }

    private ComponentEventListener(
        ComponentEventArgumentsHandler? argumentsHandler,
        AsynchronousComponentEventArgumentsHandler? asynchronousArgumentsHandler,
        bool isOnce)
    {
        ArgumentsHandler = argumentsHandler;
        AsynchronousArgumentsHandler = asynchronousArgumentsHandler;
        IsOnce = isOnce;
    }

    /// <summary>Creates a synchronous listener that receives every emitted argument.</summary>
    /// <param name="handler">The listener delegate.</param>
    /// <param name="isOnce">Whether Core invokes the listener at most once per mount.</param>
    /// <returns>The new component-event listener.</returns>
    public static ComponentEventListener ForArguments(
        ComponentEventArgumentsHandler handler,
        bool isOnce = false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new ComponentEventListener(handler, null, isOnce);
    }

    /// <summary>Creates an asynchronous listener that receives every emitted argument.</summary>
    /// <param name="handler">The task-returning listener delegate.</param>
    /// <param name="isOnce">Whether Core invokes the listener at most once per mount.</param>
    /// <returns>The new component-event listener.</returns>
    public static ComponentEventListener ForAsynchronousArguments(
        AsynchronousComponentEventArgumentsHandler handler,
        bool isOnce = false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new ComponentEventListener(null, handler, isOnce);
    }

    /// <summary>Gets the synchronous handler, or null for an asynchronous listener.</summary>
    public ComponentEventHandler? Handler { get; }

    /// <summary>Gets the asynchronous handler, or null for a synchronous listener.</summary>
    public AsynchronousComponentEventHandler? AsynchronousHandler { get; }

    /// <summary>
    /// Gets the synchronous all-arguments handler, or null for another listener shape.
    /// </summary>
    public ComponentEventArgumentsHandler? ArgumentsHandler { get; }

    /// <summary>
    /// Gets the asynchronous all-arguments handler, or null for another listener shape.
    /// </summary>
    public AsynchronousComponentEventArgumentsHandler? AsynchronousArgumentsHandler { get; }

    /// <summary>Gets whether Core invokes the listener at most once per mount.</summary>
    public bool IsOnce { get; }

    /// <summary>Gets whether this listener returns a task that Core must observe.</summary>
    public bool IsAsynchronous =>
        AsynchronousHandler is not null
        || AsynchronousArgumentsHandler is not null;
}
