using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// The result of the CSS Modules rewrite (<see cref="CssModuleRewriter.Rewrite"/>): the stylesheet with
/// every local class selector renamed to its hashed form, and the <see cref="Classes"/> map from each
/// original class name to that hashed name. Both outputs are required: the CSS carries the hashed names
/// the browser matches, and the map is what the generator turns into the typed accessor class a template
/// binds against (specified by <c>[STY-2]</c>).
/// </summary>
/// <param name="Stylesheet">
/// The stylesheet with local class selectors renamed. Feed it to <see cref="CssScopedRewriter.Rewrite"/>
/// (when the block is also <c>scoped</c>) or <see cref="CssStylesheetWriter.Write"/> to serialize —
/// both render selectors from the parsed parts, so the renamed <c>Text</c> is what they emit.
/// </param>
/// <param name="Classes">
/// The map from each original class name (without the leading <c>.</c>) to its locally-hashed name, in
/// first-seen source order. The composition-root generator emits this as the typed <c>$style</c>-equivalent
/// accessor ([V01.01.06.02]).
/// </param>
public sealed record CssModuleRewriteResult(
    CssStylesheetNode Stylesheet,
    IReadOnlyDictionary<string, string> Classes);
