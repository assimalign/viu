namespace Assimalign.Viu.Generators.Syntax;

/// <summary>
/// The two emission regions an <c>@script</c> block splits into ([V01.01.06.03.01]): the leading
/// <c>using</c> directives (plain, <c>using static</c>, and aliases) hoisted into the generated file's
/// using region <em>above the namespace</em>, and the remaining members merged into the partial-class
/// body. Each region carries its own one-based <c>.viu</c> start line — the <c>#line</c> anchor
/// <see cref="SingleFileComponentSourceEmitter"/> emits — so both keep exact <c>.viu</c> line/column
/// mapping and agree with <see cref="SingleFileComponentDiagnostics"/>'s block-to-file composition by
/// construction. A <see langword="readonly"/> <see langword="record"/> <see langword="struct"/> so it
/// rides inside the cached <see cref="SingleFileComponentModel"/> without defeating incremental caching.
/// <para>
/// C# has no single syntactic context where both a top-level <c>using</c> directive and a bare member
/// declaration are legal (usings need compilation-unit/namespace scope; fields need a type), so the block
/// is split at a <b>line boundary</b>: the using region ends at the start of the first line after the last
/// leading using directive, and the member region begins there. Both regions therefore start at <c>.viu</c>
/// column 1 and map cleanly under a per-region <c>#line</c> directive. Vue hoists a
/// <c>&lt;script setup&gt;</c> block's imports out of the render scope for the same reason
/// (https://vuejs.org/api/sfc-script-setup.html).
/// </para>
/// </summary>
/// <param name="UsingRegion">The verbatim leading <c>using</c> directives to hoist above the namespace, or <see langword="null"/> when the block has none.</param>
/// <param name="UsingRegionStartLine">The one-based <c>.viu</c> line the using region begins on (the <c>#line</c> anchor); <c>0</c> when there is no using region.</param>
/// <param name="MemberRegion">The verbatim class-body members to merge into the partial class, or <see langword="null"/> when the block contributes none (all usings, or empty/whitespace-only).</param>
/// <param name="MemberRegionStartLine">The one-based <c>.viu</c> line the member region begins on (the <c>#line</c> anchor); <c>0</c> when there is no member region.</param>
internal readonly record struct ScriptRegions(
    string? UsingRegion,
    int UsingRegionStartLine,
    string? MemberRegion,
    int MemberRegionStartLine)
{
    /// <summary>The regions of a component that declares no <c>@script</c> block: both regions absent.</summary>
    public static readonly ScriptRegions None = default;
}

/// <summary>
/// The result of analyzing an <c>@script</c> block ([V01.01.06.03]/[V01.01.06.03.01]): the
/// <see cref="Regions"/> to emit (the using-hoist + class-body member split) and the classified
/// <see cref="Bindings"/> the template compiler consumes for ref-unwrapping. Both are value-equatable, so
/// the analysis rides inside the incremental generator's cached model without defeating the cache.
/// </summary>
/// <param name="Regions">The using-hoist and class-body member regions to emit.</param>
/// <param name="Bindings">The classified top-level script members, for the template compiler's ref-unwrapping decisions.</param>
internal readonly record struct ScriptAnalysis(
    ScriptRegions Regions,
    EquatableArray<ScriptBinding> Bindings);
