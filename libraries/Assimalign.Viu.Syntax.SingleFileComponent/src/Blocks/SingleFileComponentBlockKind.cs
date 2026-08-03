namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// Discriminates the kinds of block a <c>.viu</c> single-file component can contain. The container is
/// the [V01.01.06.10] hybrid — tag-based <c>&lt;template&gt;</c>/<c>&lt;style&gt;</c>, with
/// <c>@script { }</c> and @-form custom blocks (specified by <c>[SFC-3]</c>; the grammar is normative
/// in <c>docs/FORMAT.md</c>). The same kinds classify the blocks the <c>.vue</c> compatibility parser
/// produces, so downstream stages are container-agnostic.
/// </summary>
public enum SingleFileComponentBlockKind
{
    /// <summary>A <c>&lt;template&gt;</c> block — the component's markup (legacy container: <c>@template { }</c>).</summary>
    Template = 0,

    /// <summary>A <c>@script { }</c> block — the component's C# body, merged into its partial class.</summary>
    Script = 1,

    /// <summary>A <c>&lt;style&gt;</c> block — the component's CSS (legacy container: <c>@style { }</c>).</summary>
    Style = 2,

    /// <summary>A custom block such as <c>@docs { }</c> — preserved verbatim for build tooling, never interpreted by the parser.</summary>
    Custom = 3,
}
