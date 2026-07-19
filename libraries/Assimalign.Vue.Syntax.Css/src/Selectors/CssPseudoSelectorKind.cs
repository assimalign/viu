namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// Classifies a pseudo selector by the role it plays in Vue's scoped-CSS rewrite
/// (<c>@vue/compiler-sfc</c> <c>pluginScoped.ts</c>; https://vuejs.org/api/sfc-css-features.html). The
/// three functional pseudos Vue reserves each change how (or whether) the scope attribute is applied;
/// every other pseudo-class or pseudo-element is <see cref="Normal"/> and merely passes through.
/// </summary>
public enum CssPseudoSelectorKind
{
    /// <summary>An ordinary pseudo-class or pseudo-element (<c>:hover</c>, <c>::before</c>, <c>:not(...)</c>): passed through, never the insertion point.</summary>
    Normal,

    /// <summary><c>:deep(...)</c> / <c>::v-deep(...)</c> — the inner selector escapes scoping; the attribute lands on the preceding compound, then a descendant combinator.</summary>
    Deep,

    /// <summary><c>:slotted(...)</c> / <c>::v-slotted(...)</c> — the inner selector is scoped with the slotted attribute suffix (<c>-s</c>).</summary>
    Slotted,

    /// <summary><c>:global(...)</c> / <c>::v-global(...)</c> — the inner selector opts out of scoping entirely.</summary>
    Global,
}
