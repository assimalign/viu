namespace Assimalign.Viu.State;

/// <summary>
/// Observes a state-store action before its body runs and may register completion or error hooks.
/// </summary>
/// <param name="context">The action invocation context.</param>
public delegate void StateStoreActionCallback(StateStoreActionContext context);
