namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// The two emission regions an <c>@script</c> block splits into ([V01.01.06.03.01]): the leading
/// <c>using</c> directives (plain, <c>using static</c>, and aliases) hoisted into the generated file's
/// using region <em>above the namespace</em>, and the remaining members merged into the partial-class
/// body. Each region carries its own one-based source start line and column — the <c>#line</c> anchor
/// and first-line padding <see cref="SingleFileComponentSourceEmitter"/> emits — so both keep exact
/// <c>.viu</c> or inline <c>.vue</c> line/column mapping and agree with
/// <see cref="SingleFileComponentDiagnostics"/>'s block-to-file composition by construction. A
/// <see langword="readonly"/> <see langword="record"/> <see langword="struct"/> so it rides inside the
/// cached <see cref="SingleFileComponentModel"/> without defeating incremental caching.
/// <para>
/// C# has no single syntactic context where both a top-level <c>using</c> directive and a bare member
/// declaration are legal (usings need compilation-unit/namespace scope; fields need a type), so the block
/// is split at a <b>line boundary</b>: the using region ends at the start of the first line after the last
/// leading using directive, and the member region begins there. Canonical blocks and split member regions
/// start at column 1. An inline tag-based script can begin later on its first line, so that region retains
/// its authored start column for emission padding.
/// </para>
/// </summary>
/// <param name="UsingRegion">The verbatim leading <c>using</c> directives to hoist above the namespace, or <see langword="null"/> when the block has none.</param>
/// <param name="UsingRegionStartLine">The one-based source line the using region begins on; <c>0</c> when absent.</param>
/// <param name="UsingRegionStartColumn">The one-based source column the using region begins on; <c>0</c> when absent.</param>
/// <param name="MemberRegion">The verbatim class-body members to merge into the partial class, or <see langword="null"/> when the block contributes none (all usings, or empty/whitespace-only).</param>
/// <param name="MemberRegionStartLine">The one-based source line the member region begins on; <c>0</c> when absent.</param>
/// <param name="MemberRegionStartColumn">The one-based source column the member region begins on; <c>0</c> when absent.</param>
public readonly record struct ScriptRegions(
    string? UsingRegion,
    int UsingRegionStartLine,
    int UsingRegionStartColumn,
    string? MemberRegion,
    int MemberRegionStartLine,
    int MemberRegionStartColumn)
{
    /// <summary>The regions of a component that declares no <c>@script</c> block: both regions absent.</summary>
    public static readonly ScriptRegions None = default;
}
