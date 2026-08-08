namespace Assimalign.Viu.State;

/// <summary>
/// Carries the current registry setup context into an optional <see cref="StateStore{TState}"/>
/// base constructor. Ambient state is deliberate under Viu's single-threaded event-loop model.
/// </summary>
internal static class StateStoreSetupRuntime
{
    internal static IStateContext? Current { get; set; }
}
