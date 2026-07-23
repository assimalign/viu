namespace Assimalign.Viu.State;

/// <summary>Observes a state-store mutation and its current reactive state.</summary>
/// <typeparam name="TState">The reactive state type.</typeparam>
/// <param name="mutation">Metadata describing how the state changed.</param>
/// <param name="state">The current state after the mutation.</param>
public delegate void StateStoreSubscriptionCallback<TState>(
    StateStoreMutation mutation,
    TState state)
    where TState : class;
