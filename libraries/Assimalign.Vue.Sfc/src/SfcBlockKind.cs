namespace Assimalign.Vue.Sfc;

/// <summary>
/// Discriminates the kinds of block a <c>.viu</c> single-file component can contain. The block
/// <em>semantics</em> mirror the Vue SFC specification (https://vuejs.org/api/sfc-spec.html); the
/// <c>@name { }</c> container syntax is the documented Vuecs divergence from Vue's tag-based wrappers
/// (decided 2026-07-17).
/// </summary>
public enum SfcBlockKind
{
    /// <summary>An <c>@template</c> block — the component's markup (Vue's <c>&lt;template&gt;</c>).</summary>
    Template = 0,

    /// <summary>A <c>@script</c> block — the component's C# body (Vue's <c>&lt;script&gt;</c>).</summary>
    Script = 1,

    /// <summary>A <c>@style</c> block — the component's CSS (Vue's <c>&lt;style&gt;</c>).</summary>
    Style = 2,

    /// <summary>A custom block such as <c>@docs</c> (Vue's custom blocks).</summary>
    Custom = 3,
}
