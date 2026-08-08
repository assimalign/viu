namespace Assimalign.Viu.State;

/// <summary>
/// Creates one store inside a registry-owned reactive scope. Direct delegate invocation keeps
/// setup trimming-safe and avoids reflection-backed activation. Specified by <c>[STA-1]</c> and
/// <c>[EXE-4]</c>.
/// </summary>
/// <typeparam name="TStore">The store type.</typeparam>
/// <param name="context">The store setup context.</param>
/// <returns>The newly created store.</returns>
public delegate TStore StateStoreActivator<TStore>(IStateContext context)
    where TStore : class;
