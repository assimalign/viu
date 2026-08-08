namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// One <c>v-bind()</c> CSS binding retained by the component projection ([V01.01.06.06]): the
/// hashed custom-property <see cref="Name"/> the CSS was rewritten to (<c>var(--&lt;Name&gt;)</c>) and the
/// original C# <see cref="Expression"/>. Runtime scoped-CSS and CSS-variable application are deferred
/// during the component-model migration, so the source emitter deliberately publishes no runtime seam
/// from this entry; retaining the value keeps parsing, diagnostics, and future reintroduction stable.
/// A <see langword="readonly"/>
/// <see langword="record"/> <see langword="struct"/> so it is value-equatable inside the cached model.
/// </summary>
/// <param name="Name">The hashed custom-property name (without the leading <c>--</c>).</param>
/// <param name="Expression">The original expression text the component evaluates for the property value.</param>
internal readonly record struct CssVariableBindingEntry(string Name, string Expression);
