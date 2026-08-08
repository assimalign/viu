using Assimalign.Viu.Syntax;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// One member declared in a component's <c>@script</c> block, described by
/// <see cref="ScriptBlockAnalyzer.DescribeMembers"/> for editor completion and document symbols
/// ([V01.01.06.11], superseding the language service's local reader model from [V01.01.12.07.04]).
/// Locations are <em>block-content-relative</em> — the probe wrapper's offsets are already un-shifted
/// by the shared <c>ProbePrefix</c>/<c>ProbeLineOffset</c> arithmetic — so a description depends on the
/// script text alone (cacheable by content) and the service composes document coordinates per request
/// through <see cref="SingleFileComponentDiagnostics.ComposeToFilePosition"/>, the same arithmetic the
/// emitted <c>#line</c> map uses. Editor positions and build positions therefore agree by construction.
/// </summary>
/// <param name="Name">
/// The declared identifier exactly as authored (<c>Identifier.Text</c>, keeping the <c>@</c> of a
/// verbatim identifier so completion insert text round-trips).
/// </param>
/// <param name="Kind">The neutral declaration kind: field, property, or method.</param>
/// <param name="Detail">The declared type text, or the whitespace-normalized method signature.</param>
/// <param name="DocumentationSummary">
/// The first non-empty <c>///</c> summary text line, or <see langword="null"/> when the member
/// carries no documentation comment.
/// </param>
/// <param name="MemberLocation">The block-content-relative span of the full member declaration.</param>
/// <param name="IdentifierLocation">The block-content-relative span of the declared identifier.</param>
public sealed record ScriptDeclaredMember(
    string Name,
    ScriptDeclaredMemberKind Kind,
    string Detail,
    string? DocumentationSummary,
    SourceLocation MemberLocation,
    SourceLocation IdentifierLocation);
