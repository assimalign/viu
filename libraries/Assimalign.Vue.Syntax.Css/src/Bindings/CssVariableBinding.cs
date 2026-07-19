namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// One <c>v-bind()</c> CSS binding extracted by <see cref="CssBindingRewriter"/>: the hashed custom-property
/// <see cref="Name"/> the usage was rewritten to (<c>var(--&lt;Name&gt;)</c>) paired with the original
/// <see cref="Expression"/> text and the <see cref="Location"/> of that expression. Mirrors an entry of the
/// <c>cssVars</c> list Vue's <c>@vue/compiler-sfc</c> records for <c>useCssVars</c>
/// (<c>packages/compiler-sfc/src/style/cssVars.ts</c>, https://vuejs.org/api/sfc-css-features.html#v-bind-in-css)
/// — the compile-time half of the binding that the <c>UseCssVars</c> runtime evaluates and applies as a custom
/// property.
/// </summary>
/// <param name="Name">
/// The hashed custom-property name (without the leading <c>--</c>) the usage was rewritten to. Deterministic
/// and component-scoped, so the CSS <c>var(--&lt;Name&gt;)</c> and the runtime's
/// <c>style.setProperty("--&lt;Name&gt;", …)</c> agree by construction.
/// </param>
/// <param name="Expression">The original expression text inside <c>v-bind(…)</c>, trimmed and unquoted.</param>
/// <param name="Location">
/// The block-relative source location of the expression, so a consumer compiling the expression
/// ([V01.01.06.06.01]) can map a diagnostic back onto the exact source coordinate. For a de-duplicated
/// expression it is the first occurrence's location.
/// </param>
public sealed record CssVariableBinding(string Name, string Expression, SourceLocation Location);
