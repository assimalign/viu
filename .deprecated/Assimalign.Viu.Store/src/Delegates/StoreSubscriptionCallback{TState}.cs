namespace Assimalign.Viu.Store;

/// <summary>
/// A store state-subscription callback — the C# port of the callback passed to Pinia's
/// <c>store.$subscribe((mutation, state) =&gt; { ... })</c>
/// (https://pinia.vuejs.org/core-concepts/state.html#Subscribing-to-the-state,
/// <c>packages/pinia/src/store.ts</c>). Invoked once per notification pass — a grouped
/// <see cref="Store{TState}.Patch(System.Action{TState})"/> of many members produces one call, not one
/// per member — with the <see cref="StoreMutation"/> metadata and the current state.
/// </summary>
/// <typeparam name="TState">The store's reactive state type.</typeparam>
/// <param name="mutation">The mutation metadata (store id and patch kind).</param>
/// <param name="state">The store's state after the mutation.</param>
public delegate void StoreSubscriptionCallback<TState>(StoreMutation mutation, TState state)
    where TState : class;
