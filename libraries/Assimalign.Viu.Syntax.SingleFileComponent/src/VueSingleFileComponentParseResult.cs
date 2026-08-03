namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The result of recoverably parsing a tag-based <c>.vue</c> single-file component: the descriptor plus
/// any structural diagnostics. Both are always produced, so tooling has a tree to work with even for
/// malformed source (<c>[SFC-DIAG-2]</c>).
/// </summary>
/// <param name="Descriptor">The parsed descriptor, which is produced even for malformed source.</param>
/// <param name="Errors">The recoverable structural diagnostics, in reporting order.</param>
public sealed record VueSingleFileComponentParseResult(
    VueSingleFileComponentDescriptor Descriptor,
    SyntaxList<SingleFileComponentError> Errors);
