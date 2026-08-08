namespace Assimalign.Viu.State;

/// <summary>
/// Observes an opted-in state-store action before its body runs and may register completion or
/// error hooks. Specified by <c>[STA-8]</c>.
/// </summary>
/// <param name="context">The action invocation context.</param>
public delegate void StateStoreActionCallback(StateStoreActionContext context);
