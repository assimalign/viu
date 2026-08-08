using System;

namespace Assimalign.Viu;

/// <summary>Chooses whether a failed asynchronous-component load retries or settles.</summary>
/// <remarks>Specified by <c>[BLT-14]</c>.</remarks>
/// <param name="error">The loader failure.</param>
/// <param name="retry">Retries through the same shared load operation.</param>
/// <param name="fail">Settles the operation with the original failure.</param>
/// <param name="attempts">The one-based number of load attempts.</param>
public delegate void AsynchronousComponentErrorHandler(
    Exception error,
    Action retry,
    Action fail,
    int attempts);
