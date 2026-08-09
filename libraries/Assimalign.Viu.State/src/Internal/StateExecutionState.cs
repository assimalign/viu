namespace Assimalign.Viu.State;

/// <summary>Owns ambient state-store values for one logical execution flow.</summary>
internal sealed class StateExecutionState
{
    internal IStateContext? SetupContext { get; set; }

    internal IStateStoreRegistry? ActiveRegistry { get; set; }
}
