using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Tests;

/// <summary>A configurable component definition for tests.</summary>
internal sealed class TestComponent : IComponentDefinition
{
    public string? Name { get; init; }

    public IReadOnlyList<ComponentPropertyDefinition>? Properties { get; init; }

    public IReadOnlyList<ComponentEmitDefinition>? Emits { get; init; }

    public bool InheritAttributes { get; init; } = true;

    public required Func<ComponentProperties, ComponentSetupContext, Func<VirtualNode?>> SetupFunction { get; init; }

    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        => SetupFunction(properties, context);
}

/// <summary>Captures RuntimeWarnings for assertion; restores the sink on dispose.</summary>
internal sealed class WarningCapture : IDisposable
{
    private readonly Action<string> _previous;

    public WarningCapture()
    {
        _previous = RuntimeWarnings.Sink;
        RuntimeWarnings.Sink = Messages.Add;
    }

    public List<string> Messages { get; } = [];

    public void Dispose() => RuntimeWarnings.Sink = _previous;
}
