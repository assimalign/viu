using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Resolves Viu qualified element names to the namespace tokens understood by the browser bridge.
/// This policy remains in Browser so Core never acquires HTML, SVG, or MathML knowledge.
/// </summary>
internal static class BrowserNamespacePolicy
{
    internal const string Svg = "svg";
    internal const string MathMl = "mathml";

    private const string HtmlNamespaceName = "http://www.w3.org/1999/xhtml";
    private const string ViuInternalNamespaceName = "urn:assimalign:viu:internal";
    private const string SvgNamespaceName = "http://www.w3.org/2000/svg";
    private const string MathMlNamespaceName = "http://www.w3.org/1998/Math/MathML";

    /// <summary>Returns the bridge namespace token for one complete qualified name.</summary>
    internal static string? Resolve(QualifiedName name)
    {
        if (string.Equals(name.NamespaceName, Svg, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name.NamespaceName, SvgNamespaceName, StringComparison.Ordinal))
        {
            return Svg;
        }

        if (string.Equals(name.NamespaceName, MathMl, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name.NamespaceName, MathMlNamespaceName, StringComparison.Ordinal))
        {
            return MathMl;
        }

        if (string.Equals(name.NamespaceName, HtmlNamespaceName, StringComparison.Ordinal))
        {
            return null;
        }

        // Core's KeepAlive and Suspense storage is detached renderer infrastructure. The
        // browser materializes that exact pseudo-namespace as an ordinary detached HTML node.
        if (string.Equals(
                name.NamespaceName,
                ViuInternalNamespaceName,
                StringComparison.Ordinal))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(name.NamespaceName))
        {
            throw new NotSupportedException(
                $"The Browser host does not support element namespace '{name.NamespaceName}'.");
        }

        if (string.Equals(name.LocalName, "svg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name.LocalName, "foreignObject", StringComparison.Ordinal))
        {
            return Svg;
        }

        return string.Equals(name.LocalName, "math", StringComparison.OrdinalIgnoreCase)
            ? MathMl
            : null;
    }
}
