namespace Assimalign.Viu.Router;

/// <summary>
/// Identifies why a <see cref="RouteMatcherException"/> was raised. Groups both route-definition
/// failures (raised while a route table is being built) and resolution failures (raised while a
/// location is being resolved). The code is the stable part of the contract; the exception's message
/// text is diagnostic and may be reworded.
/// </summary>
public enum RouteMatcherError
{
    /// <summary>
    /// A route path was malformed — most commonly it did not begin with <c>/</c>. Raised while the
    /// table is built by the tokenizer, which reports "Route paths should start with a /".
    /// </summary>
    InvalidRoutePath,

    /// <summary>
    /// A repeatable parameter (<c>+</c>/<c>*</c>) shared its segment with other tokens. A repeatable
    /// parameter consumes slash-separated occurrences, so it cannot coexist with sibling tokens in
    /// one segment; the tokenizer reports "A repeatable param must be alone in its segment".
    /// </summary>
    RepeatableParameterNotAlone,

    /// <summary>
    /// A custom parameter pattern opened with <c>(</c> but never closed; the tokenizer reports
    /// "Unfinished custom RegExp for param".
    /// </summary>
    UnfinishedCustomPattern,

    /// <summary>
    /// A custom parameter pattern (<c>:id(...)</c>) was not a valid regular expression; pattern
    /// compilation reports "Invalid custom RegExp for param".
    /// </summary>
    InvalidCustomPattern,

    /// <summary>
    /// A named route was requested that is not present in the table. Unlike a path that matches
    /// nothing, a missing name is a programming error, so it throws rather than resolving to an empty
    /// matched chain.
    /// </summary>
    NamedRouteNotFound,

    /// <summary>
    /// A required parameter had no value while interpolating a named route into a path; the path
    /// parser reports "Missing required param".
    /// </summary>
    MissingRequiredParameter,

    /// <summary>
    /// An array of values was supplied for a parameter that is not repeatable; the path parser
    /// reports that the provided parameter is an array but is not repeatable.
    /// </summary>
    ParameterNotRepeatable,
}
