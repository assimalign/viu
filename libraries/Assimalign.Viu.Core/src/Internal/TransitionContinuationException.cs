using System;
using System.Runtime.ExceptionServices;

namespace Assimalign.Viu;

// Carries renderer continuation failures through an authored transition-hook boundary.
internal sealed class TransitionContinuationException : Exception
{
    internal TransitionContinuationException(Exception exception)
        : base("A transition continuation failed.", exception)
    {
        DispatchInformation = ExceptionDispatchInfo.Capture(exception);
    }

    internal ExceptionDispatchInfo DispatchInformation { get; }
}
