using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// The exception raised when a route table cannot be built or a location cannot be resolved —
/// covering both the table-build failures the tokenizer and path parser raise and the resolution
/// failures the matcher raises. The specific cause is carried by <see cref="Error"/>; branch on that
/// rather than on the message text.
/// </summary>
public sealed class RouteMatcherException : Exception
{
    /// <summary>Creates a <see cref="RouteMatcherException"/>.</summary>
    /// <param name="error">The specific cause.</param>
    /// <param name="message">A human-readable description of the failure.</param>
    public RouteMatcherException(RouteMatcherError error, string message)
        : base(message)
    {
        Error = error;
    }

    /// <summary>The specific cause of the failure.</summary>
    public RouteMatcherError Error { get; }
}
