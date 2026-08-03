namespace Assimalign.Viu.Router;

/// <summary>
/// The kind of a single <see cref="PathToken"/> produced by the <see cref="PathTokenizer"/>.
/// </summary>
/// <remarks>
/// There is deliberately no grouping kind. Grouping inside a custom parameter pattern is expressed
/// by escaping the closing parenthesis, so the tokenizer never has to emit a third kind and the
/// ranker never has to score one.
/// </remarks>
internal enum PathTokenKind
{
    /// <summary>Literal path text, e.g. the <c>users</c> in <c>/users/:id</c>.</summary>
    Static,

    /// <summary>A dynamic parameter, e.g. the <c>:id</c> in <c>/users/:id</c>.</summary>
    Parameter,
}
