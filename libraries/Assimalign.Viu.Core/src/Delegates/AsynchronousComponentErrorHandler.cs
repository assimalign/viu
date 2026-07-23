using System;

namespace Assimalign.Viu;

/// <summary>Chooses whether a failed asynchronous-component load is retried or failed.</summary>
/// <param name="error">The loader failure.</param>
/// <param name="retry">Retries the load through the same shared request.</param>
/// <param name="fail">Settles the shared request with <paramref name="error"/>.</param>
/// <param name="attempts">The one-based number of load attempts made.</param>
public delegate void AsynchronousComponentErrorHandler(
    Exception error,
    Action retry,
    Action fail,
    int attempts);
