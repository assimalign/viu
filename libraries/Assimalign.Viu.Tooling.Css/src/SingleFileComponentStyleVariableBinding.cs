using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.Css;

namespace Assimalign.Viu.Tooling.Css;

/// <summary>
/// One <c>v-bind()</c> CSS binding paired with the <c>.viu</c> position where its block's content
/// begins, so a host composing per-binding diagnostics (the generator's expression compilation,
/// [V01.01.06.06.01]) can map them onto exact file coordinates. The binding itself carries its
/// block-relative <see cref="CssVariableBinding.Location"/>.
/// </summary>
/// <param name="Binding">The extracted binding — hashed custom-property name, expression, and its block-relative location.</param>
/// <param name="BlockContentStart">The file position where the owning block's content begins.</param>
public sealed record SingleFileComponentStyleVariableBinding(CssVariableBinding Binding, Position BlockContentStart);
