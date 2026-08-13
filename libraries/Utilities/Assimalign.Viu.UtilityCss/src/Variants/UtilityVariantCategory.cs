namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Groups built-in variants by the kind of later selector or at-rule work they require.
/// The parser records the category without applying CSS.
/// </summary>
public enum UtilityVariantCategory
{
    /// <summary>A configured utility prefix.</summary>
    Prefix,

    /// <summary>An arbitrary selector or at-rule.</summary>
    Arbitrary,

    /// <summary>The direct-child <c>*</c> variant.</summary>
    Child,

    /// <summary>The descendant <c>**</c> variant.</summary>
    Descendant,

    /// <summary>A pseudo-element variant.</summary>
    PseudoElement,

    /// <summary>A structural or positional pseudo-class variant.</summary>
    Structural,

    /// <summary>An element state or form-state variant.</summary>
    State,

    /// <summary>A compound variant such as <c>group-*</c>, <c>peer-*</c>, or <c>has-*</c>.</summary>
    Compound,

    /// <summary>An attribute variant such as <c>aria-*</c> or <c>data-*</c>.</summary>
    Attribute,

    /// <summary>A CSS feature-query variant.</summary>
    Supports,

    /// <summary>A viewport breakpoint variant.</summary>
    Responsive,

    /// <summary>A container-query variant.</summary>
    ContainerQuery,

    /// <summary>An environment or media-feature variant.</summary>
    Environment,

    /// <summary>A text-direction variant.</summary>
    Direction,

    /// <summary>The print-media variant.</summary>
    Print,
}
