namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The result of recoverably parsing a tag-based <c>.vue</c> single-file component. Mirrors the
/// descriptor-and-errors result of Vue 3.5's <c>@vue/compiler-sfc</c> <c>parse()</c>.
/// </summary>
/// <param name="Descriptor">The parsed descriptor, which is produced even for malformed source.</param>
/// <param name="Errors">The recoverable structural diagnostics, in reporting order.</param>
public sealed record VueSingleFileComponentParseResult(
    VueSingleFileComponentDescriptor Descriptor,
    SyntaxList<SingleFileComponentError> Errors);
