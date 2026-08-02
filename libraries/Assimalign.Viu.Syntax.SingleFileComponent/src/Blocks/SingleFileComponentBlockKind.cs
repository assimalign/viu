namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// Discriminates the kinds of block a <c>.viu</c> single-file component can contain. The block
/// <em>semantics</em> mirror the Vue SFC specification (https://vuejs.org/api/sfc-spec.html); the
/// container is the [V01.01.06.10] hybrid — tag-based <c>&lt;template&gt;</c>/<c>&lt;style&gt;</c>
/// matching Vue, with <c>@script { }</c> and @-form custom blocks as the documented Viu divergence.
/// </summary>
public enum SingleFileComponentBlockKind
{
    /// <summary>A <c>&lt;template&gt;</c> block — the component's markup (legacy container: <c>@template { }</c>).</summary>
    Template = 0,

    /// <summary>A <c>@script { }</c> block — the component's C# body (Vue's <c>&lt;script&gt;</c>).</summary>
    Script = 1,

    /// <summary>A <c>&lt;style&gt;</c> block — the component's CSS (legacy container: <c>@style { }</c>).</summary>
    Style = 2,

    /// <summary>A custom block such as <c>@docs { }</c> (Vue's custom blocks; @-form in <c>.viu</c>).</summary>
    Custom = 3,
}
