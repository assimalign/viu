namespace Assimalign.Viu.State;

/// <summary>
/// Carries the current registry setup context into an optional <see cref="StateStore{TState}"/>
/// base constructor. Browser execution shares one event-loop value; request-oriented hosts isolate
/// it by logical execution flow.
/// </summary>
internal static class StateStoreSetupRuntime
{
    internal static IStateContext? Current
    {
        get => StateExecutionIsolation.Current.SetupContext;
        set => StateExecutionIsolation.Current.SetupContext = value;
    }
}
