namespace Assimalign.Viu.Router;

/// <summary>
/// Identifies why a <see cref="RouteMatcherException"/> was raised. Groups both route-definition
/// failures (raised while a route table is being built) and resolution failures (raised while a
/// location is being resolved). The C# port of the matcher-relevant subset of vue-router's
/// <c>ErrorTypes</c> (<c>packages/router/src/errors.ts</c>) plus the descriptive <c>Error</c>s
/// thrown by the tokenizer and path parser (<c>packages/router/src/matcher/</c>).
/// </summary>
public enum RouteMatcherError
{
    /// <summary>
    /// A route path was malformed — most commonly it did not begin with <c>/</c>. Raised while the
    /// table is built. Mirrors the tokenizer's "Route paths should start with a /" error.
    /// </summary>
    InvalidRoutePath,

    /// <summary>
    /// A repeatable parameter (<c>+</c>/<c>*</c>) shared its segment with other tokens, which
    /// vue-router forbids. Mirrors the tokenizer's "A repeatable param must be alone in its
    /// segment" error.
    /// </summary>
    RepeatableParameterNotAlone,

    /// <summary>
    /// A custom parameter pattern opened with <c>(</c> but never closed. Mirrors the tokenizer's
    /// "Unfinished custom RegExp for param" error.
    /// </summary>
    UnfinishedCustomPattern,

    /// <summary>
    /// A custom parameter pattern (<c>:id(...)</c>) was not a valid regular expression. Mirrors the
    /// "Invalid custom RegExp for param" error raised by <c>tokensToParser</c>.
    /// </summary>
    InvalidCustomPattern,

    /// <summary>
    /// A named route was requested that is not present in the table. Mirrors vue-router's
    /// <c>MATCHER_NOT_FOUND</c>.
    /// </summary>
    NamedRouteNotFound,

    /// <summary>
    /// A required parameter had no value while interpolating a named route into a path. Mirrors the
    /// path parser's "Missing required param" error.
    /// </summary>
    MissingRequiredParameter,

    /// <summary>
    /// An array of values was supplied for a parameter that is not repeatable. Mirrors the path
    /// parser's "Provided param ... is an array but it is not repeatable" error.
    /// </summary>
    ParameterNotRepeatable,
}
