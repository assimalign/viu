using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// The exception raised when a route table cannot be built or a location cannot be resolved. The
/// C# port of the errors vue-router surfaces from its matcher — both the descriptive
/// <c>Error</c>s thrown by the tokenizer/path parser
/// (<c>packages/router/src/matcher/pathTokenizer.ts</c>,
/// <c>pathParserRanker.ts</c>) and the coded <c>MATCHER_NOT_FOUND</c> navigation error
/// (<c>packages/router/src/errors.ts</c>). The specific cause is carried by <see cref="Error"/>.
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
