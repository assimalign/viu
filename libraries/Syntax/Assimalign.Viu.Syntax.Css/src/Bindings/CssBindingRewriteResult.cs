using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// The result of the <c>v-bind()</c> rewrite (<see cref="CssBindingRewriter.Rewrite"/>): the stylesheet
/// with every <c>v-bind(expr)</c> in a declaration value replaced by <c>var(--&lt;hash&gt;)</c>, the
/// <see cref="Bindings"/> collected for the generated component metadata, and any
/// <see cref="Diagnostics"/> for malformed usages. The rewrite is deliberately two-output: the CSS text
/// alone cannot tell the generator which expressions to watch, and the binding list alone cannot tell
/// the stylesheet which property to read.
/// </summary>
/// <param name="Stylesheet">
/// The stylesheet with rewritten declaration values. Feed it to <see cref="CssScopedRewriter.Rewrite"/>
/// (when the block is also <c>scoped</c>) or <see cref="CssStylesheetWriter.Write"/> to serialize.
/// </param>
/// <param name="Bindings">
/// The distinct bindings, in first-seen source order — each a hashed custom-property name paired with the
/// original expression the runtime evaluates.
/// </param>
/// <param name="Diagnostics">
/// Recoverable diagnostics for malformed usages (an unterminated <c>v-bind(</c> or an empty
/// <c>v-bind()</c>), located on the offending declaration; empty when every usage was well formed.
/// </param>
public sealed record CssBindingRewriteResult(
    CssStylesheetNode Stylesheet,
    IReadOnlyList<CssVariableBinding> Bindings,
    IReadOnlyList<CssError> Diagnostics);
