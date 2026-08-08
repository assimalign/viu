using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Defines explicitly registered asynchronous components.</summary>
/// <remarks>
/// Definitions are registration/request facades and never activate a target directly. Specified
/// by <c>[BLT-14]</c>.
/// </remarks>
public static class AsynchronousComponents
{
    /// <summary>Defines an asynchronous component under an authored wrapper type.</summary>
    /// <typeparam name="TComponent">The stable wrapper component type.</typeparam>
    /// <param name="loader">The target-identity loader.</param>
    /// <param name="name">The optional explicit registration name.</param>
    /// <returns>The reusable asynchronous definition.</returns>
    public static AsynchronousComponentDefinition Define<TComponent>(
        AsynchronousComponentLoader loader,
        string? name = null)
        where TComponent : class, IComponent
    {
        return Define(typeof(TComponent), loader, name);
    }

    /// <summary>Defines an asynchronous component under an authored wrapper type.</summary>
    /// <typeparam name="TComponent">The stable wrapper component type.</typeparam>
    /// <param name="options">The loading, failure, timing, and retry policy.</param>
    /// <param name="name">The optional explicit registration name.</param>
    /// <returns>The reusable asynchronous definition.</returns>
    public static AsynchronousComponentDefinition Define<TComponent>(
        AsynchronousComponentOptions options,
        string? name = null)
        where TComponent : class, IComponent
    {
        return Define(typeof(TComponent), options, name);
    }

    /// <summary>Defines an asynchronous component from an authored wrapper type and loader.</summary>
    /// <param name="componentType">The stable wrapper <see cref="IComponent"/> type.</param>
    /// <param name="loader">The target-identity loader.</param>
    /// <param name="name">The optional explicit registration name.</param>
    /// <returns>The reusable asynchronous definition.</returns>
    public static AsynchronousComponentDefinition Define(
        Type componentType,
        AsynchronousComponentLoader loader,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(loader);
        return Define(
            componentType,
            new AsynchronousComponentOptions { Loader = loader },
            name);
    }

    /// <summary>Defines an asynchronous component from an authored wrapper type and policy.</summary>
    /// <param name="componentType">The stable wrapper <see cref="IComponent"/> type.</param>
    /// <param name="options">The loading, failure, timing, and retry policy.</param>
    /// <param name="name">The optional explicit registration name.</param>
    /// <returns>The reusable asynchronous definition.</returns>
    public static AsynchronousComponentDefinition Define(
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
