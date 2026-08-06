namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The result of parsing a <c>.viu</c> file: the <see cref="SingleFileComponentDescriptor"/> plus any recoverable
/// diagnostics. Both are always produced — a descriptor accompanies even a diagnostic-laden parse, so
/// editor tooling always has a tree to work with (<c>[SFC-DIAG-2]</c>). Value-equatable so identical
/// input yields an equal result.
/// </summary>
/// <param name="Descriptor">The parsed descriptor (always produced, even for malformed input).</param>
/// <param name="Errors">
/// The recoverable diagnostics, in source order; empty when the file is fully well-formed. Despite the
/// name (kept for compatibility), the list carries <b>all severities</b> since [V01.01.06.10]: the
/// legacy-container migration diagnostics (1015/1016) are warnings — check
/// <see cref="Diagnostic.Severity"/> before treating an entry as fatal.
/// </param>
public sealed record SingleFileComponentParseResult(SingleFileComponentDescriptor Descriptor, SyntaxList<SingleFileComponentError> Errors);
