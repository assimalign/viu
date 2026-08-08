using System;
using System.Collections.Generic;
using System.Threading;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>Provides the default immutable application composition and observable lifetime state.</summary>
/// <remarks>
/// Constructing a context snapshots the option references. One
/// <see cref="ApplicationLifetime"/> may claim it; a second attachment is rejected. Specified by
/// <c>[APP-1]</c>, <c>[APP-2]</c>, and <c>[APP-6]</c>.
/// </remarks>
public sealed class ApplicationContext : IApplicationContext
{
    private bool _isLifetimeAttached;

    /// <summary>Initializes a context from the current application options.</summary>
    /// <param name="options">The options whose borrowed values are captured.</param>
    /// <exception cref="InvalidOperationException">No root component was configured.</exception>
    public ApplicationContext(ApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        RootComponent = options.RootComponent
            ?? throw new InvalidOperationException(
                "Configure ApplicationOptions.RootComponent before building the application.");
        Components = options.Components
            ?? throw new InvalidOperationException(
                "Configure ApplicationOptions.Components with a component resolver.");
        State = options.State;
        Services = State is null
            ? options.Services
            : new ApplicationServiceProvider(options.Services, State);
        ErrorHandler = options.ErrorHandler;
        WarnHandler = options.WarnHandler;
        EventObserver = options.EventObserver;
        Directives = options.Directives;
    }

    /// <inheritdoc/>
    public bool IsRunning { get; private set; }

    /// <inheritdoc/>
    public CancellationToken Stopping { get; private set; }

    /// <inheritdoc/>
    public VirtualNode RootComponent { get; }

    /// <inheritdoc/>
    public IComponentFactory Components { get; }

    /// <inheritdoc/>
    public IServiceProvider? Services { get; }

    /// <inheritdoc/>
    public IStateStoreRegistry? State { get; }

    /// <inheritdoc/>
    public IDirectiveResolver? Directives { get; }

    /// <inheritdoc/>
    public Action<Exception, ComponentContext?, string>? ErrorHandler { get; }

    /// <inheritdoc/>
    public Action<string>? WarnHandler { get; }

    /// <inheritdoc/>
    public Action<ComponentContext, string, IReadOnlyList<object?>>? EventObserver { get; }

    internal void ClaimLifetime(CancellationToken stopping)
    {
        if (_isLifetimeAttached)
        {
            throw new InvalidOperationException(
                "An application context can be attached to only one application lifetime.");
        }

        _isLifetimeAttached = true;
        Stopping = stopping;
    }

    internal void SetIsRunning(bool isRunning) => IsRunning = isRunning;
}
