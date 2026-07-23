namespace Assimalign.Viu.State;

/// <summary>Creates one application-scoped state store inside its reactive scope.</summary>
/// <typeparam name="TStore">The state store type.</typeparam>
/// <param name="context">The state setup context.</param>
/// <returns>The state store.</returns>
public delegate TStore StateStoreSetup<TStore>(IStateContext context)
    where TStore : class;

