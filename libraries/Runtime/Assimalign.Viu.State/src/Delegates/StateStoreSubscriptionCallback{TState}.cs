namespace Assimalign.Viu.State;

/// <summary>
/// Observes a state-store mutation and its current reactive state. Specified by <c>[STA-5]</c> and
/// <c>[STA-7]</c>.
/// </summary>
/// <typeparam name="TState">The reactive state type.</typeparam>
/// <param name="mutation">Metadata describing how the state changed.</param>
/// <param name="state">The stable live state after the mutation.</param>
public delegate void StateStoreSubscriptionCallback<TState>(
    StateStoreMutation mutation,
    TState state)
    where TState : class;
