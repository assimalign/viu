namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// How a template identifier is bound to its component: the source (data, props, <c>&lt;script setup&gt;</c>
/// state, options-API members) and, for setup state, whether it is a reactive reference that the compiler must
/// unwrap. The C# port of Vue 3.5's <c>BindingTypes</c> enum (<c>@vue/compiler-core</c> <c>options.ts</c>).
/// </summary>
/// <remarks>
/// The component/setup source model produces the <see cref="BindingMetadata"/> that maps each member name to
/// one of these; expression and scope analysis ([V01.01.05.04]) reads it to decide how to rewrite an
/// identifier for code generation ([V01.01.05.05]). Unlike Vue — which relies on the JavaScript
/// <c>Proxy</c>-backed render context for automatic ref unwrapping — Viu has no runtime proxy, so the
/// compiler alone decides where a <c>Ref&lt;T&gt;.Value</c> access is inserted, driven by these classifications.
/// See https://vuejs.org/guide/essentials/reactivity-fundamentals.html#ref-unwrapping-in-templates.
/// </remarks>
public enum BindingType
{
    /// <summary>A member returned from the options-API <c>data()</c> (upstream <c>DATA</c>).</summary>
    Data,

    /// <summary>A declared component prop (upstream <c>PROPS</c>).</summary>
    Property,

    /// <summary>
    /// A prop accessed through a destructure alias whose real prop name differs (upstream <c>PROPS_ALIASED</c>);
    /// the real name is resolved through <see cref="BindingMetadata.GetPropertyAlias"/>.
    /// </summary>
    PropertyAliased,

    /// <summary>A <c>let</c> binding declared in <c>&lt;script setup&gt;</c> (upstream <c>SETUP_LET</c>).</summary>
    SetupLet,

    /// <summary>
    /// A <c>const</c> binding in <c>&lt;script setup&gt;</c> that is provably not a reference — a literal, a
    /// function, or another non-reactive value (upstream <c>SETUP_CONST</c>). Never unwrapped.
    /// </summary>
    SetupConstant,

    /// <summary>
    /// A <c>const</c> binding initialized from <c>reactive(...)</c> in <c>&lt;script setup&gt;</c> (upstream
    /// <c>SETUP_REACTIVE_CONST</c>). Reactive but not a reference, so never unwrapped.
    /// </summary>
    SetupReactiveConstant,

    /// <summary>
    /// A <c>&lt;script setup&gt;</c> binding that may or may not be a reference at runtime (upstream
    /// <c>SETUP_MAYBE_REF</c>); reads are guarded through the <c>unref</c> runtime helper.
    /// </summary>
    SetupMaybeReference,

    /// <summary>
    /// A <c>&lt;script setup&gt;</c> binding that is definitely a <c>Ref&lt;T&gt;</c> (upstream <c>SETUP_REF</c>);
    /// the compiler inserts <c>.Value</c> in both read and write positions.
    /// </summary>
    SetupReference,

    /// <summary>
    /// A member resolved by the options API (methods, computed, injections) that is not otherwise classified
    /// (upstream <c>OPTIONS</c>).
    /// </summary>
    Options,

    /// <summary>
    /// A compile-time literal constant, e.g. an inlined enum member (upstream <c>LITERAL_CONST</c>). Never
    /// unwrapped and eligible for the strongest constant folding.
    /// </summary>
    LiteralConstant,
}
