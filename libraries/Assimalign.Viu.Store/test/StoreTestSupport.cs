using System;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.Store.Tests;

/// <summary>
/// A minimal setup-style store used across the store tests: a counter whose getter is a
/// <see cref="Computed{T}"/> and whose change is observed by a scope-owned <c>Watch</c>, so run
/// counts can prove scope teardown. The computed and watcher are created in the constructor, which
/// runs inside the store's effect scope, exactly as members declared in a Pinia setup store are.
/// </summary>
internal sealed class CounterStore
{
    public CounterStore()
    {
        Count = Reactive.Reference(0);
        Doubled = Reactive.Computed(() => Count.Value * 2);
        // A scope-owned watcher on the computed getter (sync flush, not immediate): fires on every
        // change until the store's scope stops.
        Reactive.Watch(() => Doubled.Value, (_, _, _) => WatcherRuns++);
    }

    /// <summary>The reactive state ref (a Pinia state member).</summary>
    public Reference<int> Count { get; }

    /// <summary>The computed getter over <see cref="Count"/> (a Pinia getter).</summary>
    public Computed<int> Doubled { get; }

    /// <summary>How many times the scope-owned watcher fired (a per-instance run count).</summary>
    public int WatcherRuns { get; private set; }

    /// <summary>Mutates state (a Pinia action).</summary>
    public void Increment() => Count.Value++;
}

/// <summary>
/// A component definition whose <c>Setup</c> is supplied by a delegate — the store tests need to run
/// code with a current <see cref="ComponentInstance"/> and app context. Mirrors the RuntimeCore test
/// helper of the same shape.
/// </summary>
internal sealed class SetupComponent : IComponentDefinition
{
    public required Func<Func<VirtualNode?>> SetupFunction { get; init; }

    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        => SetupFunction();
}
