namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// Classifies parsed functional selector syntax. Global selectors are recognized by CSS Modules;
/// deep and slotted spellings remain represented for source preservation, without selector rewriting.
/// Scoped styles were removed by <c>[V01.01.06.17]</c>.
/// </summary>
public enum CssPseudoSelectorKind
{
    /// <summary>An ordinary pseudo-class or pseudo-element (<c>:hover</c>, <c>::before</c>, <c>:not(...)</c>): preserved as authored.</summary>
    Normal,

    /// <summary><c>:deep(...)</c> / <c>::v-deep(...)</c> — retained parsed syntax with no Viu rewrite.</summary>
    Deep,

    /// <summary><c>:slotted(...)</c> / <c>::v-slotted(...)</c> — retained parsed syntax with no Viu rewrite.</summary>
    Slotted,

    /// <summary><c>:global(...)</c> / <c>::v-global(...)</c> — the inner selector opts out of CSS Modules class renaming.</summary>
    Global,
}
