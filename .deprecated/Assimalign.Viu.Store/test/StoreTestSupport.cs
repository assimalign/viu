using System;
using System.Threading.Tasks;

using Assimalign.Viu;

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
/// code with a current <see cref="ComponentInstance"/> and app context. Mirrors the Core test
/// helper of the same shape.
/// </summary>
internal sealed class SetupComponent : IComponent
{
    public required Func<ComponentSetup> SetupFunction { get; init; }

    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
        => SetupFunction();
}

/// <summary>
/// The source-generated reactive state for the <see cref="Store{TState}"/> test stores: per-member
/// track/trigger (the Roslyn <c>[Reactive]</c> generator) so a getter over one member is unaffected by
/// a change to another. Upstream parity: https://pinia.vuejs.org/core-concepts/state.html.
/// </summary>
[Reactive]
internal partial class CounterState
{
    /// <summary>The counter value (a Pinia state member).</summary>
    public partial int Count { get; set; }

    /// <summary>The increment step (a second, independently tracked state member).</summary>
    public partial int Step { get; set; }
}

/// <summary>
/// A setup-style store built on <see cref="Store{TState}"/>: a computed getter over one state member
/// (with a run counter to pin caching), actions routed through <c>RunAction</c> so <c>OnAction</c>
/// observes them, and the factory/applier constructor so <c>Reset</c> and object-form <c>Patch</c>
/// work. Upstream parity: Pinia setup stores (https://pinia.vuejs.org/core-concepts/).
/// </summary>
internal sealed class ModelCounterStore : Store<CounterState>
{
    public ModelCounterStore()
        : base(
            "model-counter",
            static () => new CounterState { Count = 0, Step = 1 },
            static (target, source) => { target.Count = source.Count; target.Step = source.Step; })
    {
        Doubled = Reactive.Computed(() =>
        {
            DoubledRuns++;
            return State.Count * 2;
        });
    }

    /// <summary>A computed getter over <see cref="CounterState.Count"/> only (a Pinia getter).</summary>
    public Computed<int> Doubled { get; }

    /// <summary>How many times the <see cref="Doubled"/> getter body ran (pins computed caching).</summary>
    public int DoubledRuns { get; private set; }

    /// <summary>Increments <see cref="CounterState.Count"/> by <see cref="CounterState.Step"/> (an action).</summary>
    public void Increment() => RunAction(nameof(Increment), () => State.Count += State.Step);

    /// <summary>A value-returning action: increments and returns the new count.</summary>
    public int IncrementBy(int amount) => RunAction(nameof(IncrementBy), () =>
    {
        State.Count += amount;
        return State.Count;
    });

    /// <summary>An asynchronous value-returning action: awaits, increments, returns the new count.</summary>
    public Task<int> IncrementByAsync(int amount) => RunActionAsync(nameof(IncrementByAsync), async () =>
    {
        await Task.Yield();
        State.Count += amount;
        return State.Count;
    });

    /// <summary>An action that always throws, to exercise the <c>OnError</c> hook.</summary>
    public void Explode() => RunAction(nameof(Explode), () => throw new InvalidOperationException("boom"));
}

/// <summary>
/// A store built with the state-only constructor (no factory/applier), so <see cref="Store{TState}.Reset"/>
/// and object-form <see cref="Store{TState}.Patch(CounterState)"/> are unsupported and throw — parity
/// with a Pinia setup store that does not implement <c>$reset</c>.
/// </summary>
internal sealed class NoResetStore : Store<CounterState>
{
    public NoResetStore()
        : base("no-reset", new CounterState { Count = 0, Step = 1 })
    {
    }

    /// <summary>Increments via the mutator-form patch (the only patch form available without an applier).</summary>
    public void Increment() => Patch(state => state.Count += state.Step);
}
