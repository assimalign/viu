using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// The built-in directive transform table the transform pipeline applies, keyed by directive name. The C#
/// port of the merged base preset (<c>@vue/compiler-core</c> <c>compile.ts</c>: <c>on</c>/<c>bind</c>/
/// <c>model</c>) and <c>@vue/compiler-dom</c>'s <c>DOMDirectiveTransforms</c> (<c>cloak</c>/<c>html</c>/
/// <c>text</c>/<c>model</c>/<c>on</c>/<c>show</c>). Vuecs targets the DOM, so the DOM overrides of <c>on</c>
/// and <c>model</c> are the defaults. User-supplied transforms in <see cref="TransformOptions"/> override
/// these by name.
/// </summary>
internal static class DomDirectiveTransforms
{
    /// <summary>Builds the built-in directive transform table.</summary>
    public static IReadOnlyDictionary<string, DirectiveTransform> Create() => new Dictionary<string, DirectiveTransform>
    {
        ["bind"] = VBindTransform.Transform,
        ["on"] = VOnTransform.Transform,
        ["model"] = VModelTransform.Transform,
        ["show"] = VShowTransform.Transform,
        ["html"] = VHtmlTransform.Transform,
        ["text"] = VTextTransform.Transform,
        ["cloak"] = NoopDirectiveTransform.Transform,
    };
}
