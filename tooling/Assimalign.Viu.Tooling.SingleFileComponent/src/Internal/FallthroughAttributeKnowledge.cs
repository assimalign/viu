using System;
using System.Collections.Generic;

using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Tooling.SingleFileComponent;

/// <summary>
/// Decides whether an undeclared attribute on a component usage is a plausible <em>fallthrough</em>
/// attribute — one a developer meant to land on the component's rendered root [CMP-17] — rather than a
/// misspelled parameter. Fallthrough is a specified feature, so an undeclared attribute can never be an
/// error; this predicate is what keeps the warning off the ones that are obviously deliberate
/// ([SFC-USE-2]).
/// <para>
/// The set is intentionally generous: a missed misspelling costs a developer nothing, while a warning on
/// a legitimate attribute costs every build that uses it.
/// </para>
/// </summary>
internal static class FallthroughAttributeKnowledge
{
    // Attributes the render pipeline itself consumes or merges, plus globals absent from the shared
    // HTML attribute table (which tracks element-specific attributes and predates ARIA/custom-element
    // globals). Ordinal, matching the template compiler's own attribute handling.
    private static readonly HashSet<string> AlwaysAllowed = new(StringComparer.Ordinal)
    {
        "key",
        "ref",
        "ref_for",
        "ref_key",
        "is",
        "class",
        "style",
        "id",
        "role",
        "part",
        "exportparts",
        "slot",
        "popover",
        "popovertarget",
        "popovertargetaction",
        "inputmode",
        "enterkeyhint",
        "nonce",
        "autocorrect",
        "writingsuggestions",
        "virtualkeyboardpolicy",
        "elementtiming",
        "itemid",
        "itemprop",
        "itemref",
        "itemscope",
        "itemtype",
        "xmlns",
    };

    /// <summary>
    /// Whether <paramref name="name"/> reads as an attribute meant to fall through to the rendered root
    /// rather than as an attempt to supply a parameter.
    /// </summary>
    /// <param name="name">The attribute name exactly as authored.</param>
    /// <returns>True when the name must not be reported as an unknown parameter.</returns>
    public static bool IsFallthroughCandidate(string name)
    {
        if (name.Length == 0)
        {
            return true;
        }

        if (AlwaysAllowed.Contains(name))
        {
            return true;
        }

        // Author-defined namespaced attributes: `data-*` and `aria-*` are unbounded by design, and a
        // vendor prefix (`x-`, `hx-`, …) is a deliberate custom attribute, never a C# property name.
        if (name.IndexOf('-') > 0)
        {
            return true;
        }

        // `xml:lang`, `xlink:href`, and the like.
        if (name.IndexOf(':') >= 0)
        {
            return true;
        }

        return CompilerDomKnowledge.IsKnownHtmlAttribute(name)
            || CompilerDomKnowledge.IsKnownSvgAttribute(name);
    }
}
