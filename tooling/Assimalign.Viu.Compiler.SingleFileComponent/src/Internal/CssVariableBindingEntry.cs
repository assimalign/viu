namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// One <c>v-bind()</c> CSS binding recorded in the generated component metadata ([V01.01.06.06]): the
/// hashed custom-property <see cref="Name"/> the CSS was rewritten to (<c>var(--&lt;Name&gt;)</c>) and the
/// original C# <see cref="Expression"/> the component evaluates. The emitter turns these into the
/// <c>ApplyCssVariables</c> seam that calls the <c>UseCssVariables</c> runtime with a getter mapping each
/// name to its evaluated value — the compile-time half of <c>v-bind()</c>-in-CSS reactivity, whose runtime
/// half re-applies the values on the next flush. A <see langword="readonly"/>
/// <see langword="record"/> <see langword="struct"/> so it is value-equatable inside the cached model.
/// </summary>
/// <param name="Name">The hashed custom-property name (without the leading <c>--</c>).</param>
/// <param name="Expression">The original expression text the component evaluates for the property value.</param>
internal readonly record struct CssVariableBindingEntry(string Name, string Expression);
