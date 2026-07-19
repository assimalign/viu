namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The result of parsing a <c>.viu</c> file: the <see cref="SingleFileComponentDescriptor"/> plus any recoverable
/// diagnostics. Mirrors the <c>{ descriptor, errors }</c> return of Vue 3.5's <c>parse()</c>
/// (<c>@vue/compiler-sfc</c> <c>parse.ts</c>). Value-equatable so identical input yields an equal result.
/// </summary>
/// <param name="Descriptor">The parsed descriptor (always produced, even for malformed input).</param>
/// <param name="Errors">The recoverable diagnostics, in source order; empty when the file is well-formed.</param>
public sealed record SingleFileComponentParseResult(SingleFileComponentDescriptor Descriptor, SyntaxList<SingleFileComponentError> Errors);
