namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// How a template identifier is bound to its component: the source (data, props, <c>&lt;script setup&gt;</c>
/// state, options-API members) and, for setup state, whether it is a reactive reference that the compiler must
/// unwrap.
/// </summary>
/// <remarks>
/// The component/setup source model produces the <see cref="BindingMetadata"/> that maps each member name to
/// one of these; expression and scope analysis ([V01.01.05.04]) reads it to decide how to rewrite an
/// identifier for code generation ([V01.01.05.05]). Because there is no runtime proxy to auto-unwrap a
/// reference (<c>[RCT-8]</c>), the compiler alone decides where a <c>.Value</c> access is inserted, and
/// this classification is the input to that decision. The read and write form for each member of this
/// enum is normative in <c>[SFC-6]</c>.
/// </remarks>
public enum BindingType
{
    /// <summary>A member returned from the options-API <c>data()</c>.</summary>
    Data,

    /// <summary>A declared component prop.</summary>
    Property,

    /// <summary>
    /// A prop accessed through a destructure alias whose real prop name differs;
    /// the real name is resolved through <see cref="BindingMetadata.GetPropertyAlias"/>.
    /// </summary>
    PropertyAliased,

    /// <summary>A <c>let</c> binding declared in <c>&lt;script setup&gt;</c>.</summary>
    SetupLet,

    /// <summary>
    /// A <c>const</c> binding in <c>&lt;script setup&gt;</c> that is provably not a reference — a literal, a
    /// function, or another non-reactive value. Never unwrapped.
    /// </summary>
    SetupConstant,

    /// <summary>
    /// A <c>const</c> binding initialized from <c>reactive(...)</c> in <c>&lt;script setup&gt;</c>.
    /// Reactive but not a reference, so never unwrapped.
    /// </summary>
    SetupReactiveConstant,

    /// <summary>
    /// A <c>&lt;script setup&gt;</c> binding that may or may not be a reference at runtime; reads are
    /// guarded through the <c>unref</c> runtime helper, writes go straight to <c>.Value</c>.
    /// </summary>
    SetupMaybeReference,

    /// <summary>
    /// A <c>&lt;script setup&gt;</c> binding that is definitely a <c>Ref&lt;T&gt;</c>;
    /// the compiler inserts <c>.Value</c> in both read and write positions.
    /// </summary>
    SetupReference,

    /// <summary>
    /// A member resolved by the options API (methods, computed, injections) that is not otherwise
    /// classified.
    /// </summary>
    Options,

    /// <summary>
    /// A compile-time literal constant, e.g. an inlined enum member. Never
    /// unwrapped and eligible for the strongest constant folding.
    /// </summary>
    LiteralConstant,
}
