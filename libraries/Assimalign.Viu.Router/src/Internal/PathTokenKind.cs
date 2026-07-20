namespace Assimalign.Viu.Router;

/// <summary>
/// The kind of a single <see cref="PathToken"/> produced by the <see cref="PathTokenizer"/>.
/// The C# port of vue-router's <c>TokenType</c>
/// (<c>packages/router/src/matcher/pathTokenizer.ts</c>).
/// </summary>
/// <remarks>
/// Upstream also declares a <c>Group</c> member, but the current vue-router tokenizer never emits
/// it (grouping is expressed by escaping the closing parenthesis inside a custom pattern instead),
/// so the port models only the two kinds the tokenizer actually produces.
/// </remarks>
internal enum PathTokenKind
{
    /// <summary>Literal path text, e.g. the <c>users</c> in <c>/users/:id</c> (upstream <c>Static</c>).</summary>
    Static,

    /// <summary>A dynamic parameter, e.g. the <c>:id</c> in <c>/users/:id</c> (upstream <c>Param</c>).</summary>
    Parameter,
}
