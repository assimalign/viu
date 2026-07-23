using System;

namespace Assimalign.Viu;

/// <summary>
/// Defines factory-registered asynchronous components.
/// </summary>
/// <remarks>
/// This is Viu's trimming-safe counterpart to Vue 3.5's <c>defineAsyncComponent</c>:
/// https://vuejs.org/guide/components/async.html. A definition is an explicit registration/request
/// facade; it is not an activated component and does not resolve services.
/// </remarks>
public static class AsynchronousComponents
{
    /// <summary>Defines an asynchronous component under a compile-time wrapper identity.</summary>
    /// <typeparam name="TIdentity">
    /// The stable marker type registered for the asynchronous wrapper.
    /// </typeparam>
    /// <param name="loader">The target-identity loader.</param>
    /// <param name="name">The optional component-factory name for the wrapper.</param>
    /// <returns>The definition whose registration is added to the application component factory.</returns>
    public static AsynchronousComponentDefinition DefineAsynchronousComponent<TIdentity>(
        AsynchronousComponentLoader loader,
        string? name = null)
        where TIdentity : class
    {
        return DefineAsynchronousComponent(typeof(TIdentity), loader, name);
    }

    /// <summary>Defines an asynchronous component under a compile-time wrapper identity.</summary>
    /// <typeparam name="TIdentity">
    /// The stable marker type registered for the asynchronous wrapper.
    /// </typeparam>
    /// <param name="options">The loading, error, timing, and retry policy.</param>
    /// <param name="name">The optional component-factory name for the wrapper.</param>
    /// <returns>The definition whose registration is added to the application component factory.</returns>
    public static AsynchronousComponentDefinition DefineAsynchronousComponent<TIdentity>(
        AsynchronousComponentOptions options,
        string? name = null)
        where TIdentity : class
    {
        return DefineAsynchronousComponent(typeof(TIdentity), options, name);
    }

    /// <summary>Defines an asynchronous component from a loader.</summary>
    /// <param name="componentType">
    /// The stable type identity under which the wrapper is registered. The identity is supplied
    /// explicitly and is never discovered or activated through reflection.
    /// </param>
    /// <param name="loader">The target-identity loader.</param>
    /// <param name="name">The optional component-factory name for the wrapper.</param>
    /// <returns>The definition whose registration is added to the application component factory.</returns>
    public static AsynchronousComponentDefinition DefineAsynchronousComponent(
        Type componentType,
        AsynchronousComponentLoader loader,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(loader);
        return DefineAsynchronousComponent(
            componentType,
            new AsynchronousComponentOptions
            {
                Loader = loader,
            },
            name);
    }

    /// <summary>Defines an asynchronous component from an options object.</summary>
    /// <param name="componentType">
    /// The stable type identity under which the wrapper is registered.
    /// </param>
    /// <param name="options">The loading, error, timing, and retry policy.</param>
    /// <param name="name">The optional component-factory name for the wrapper.</param>
    /// <returns>The definition whose registration is added to the application component factory.</returns>
    public static AsynchronousComponentDefinition DefineAsynchronousComponent(
        Type componentType,
        AsynchronousComponentOptions options,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Loader);
        if (name is not null)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
        }

        if (options.Delay < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The asynchronous component delay cannot be negative.");
        }

        if (options.Timeout is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The asynchronous component timeout cannot be negative.");
        }

        return new AsynchronousComponentDefinition(componentType, options, name);
    }
}
