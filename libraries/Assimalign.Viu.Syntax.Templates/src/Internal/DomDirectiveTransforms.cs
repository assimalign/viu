using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The built-in directive transform table the transform pipeline applies, keyed by directive name. It
/// covers <c>bind</c>, <c>cloak</c>, <c>html</c>, <c>model</c>, <c>on</c>, <c>show</c>, and <c>text</c>.
/// Viu targets the DOM, so the DOM-aware forms of <c>on</c> and <c>model</c> are the defaults rather
/// than the platform-neutral ones. User-supplied transforms in <see cref="TransformOptions"/> override
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
