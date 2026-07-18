using System;
using System.Linq;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// Configures <see cref="TemplateParser"/>. The C# port of the parse-relevant members of Vue 3.5's
/// <c>ParserOptions</c> (<c>@vue/compiler-core</c> <c>options.ts</c>). The defaults reproduce Vue's
/// <c>defaultParserOptions</c> (platform-agnostic base mode); <see cref="CreateHtml"/> reproduces
/// <c>@vue/compiler-dom</c>'s <c>parserOptions</c> (namespace inference, void/pre/RCDATA tags).
/// </summary>
public sealed class ParserOptions
{
    /// <summary>The parse mode selecting special-element behaviour. Defaults to <see cref="TemplateParseMode.Base"/>.</summary>
    public TemplateParseMode Mode { get; set; } = TemplateParseMode.Base;

    /// <summary>The root namespace. Defaults to <see cref="ElementNamespace.Html"/>.</summary>
    public ElementNamespace RootNamespace { get; set; } = ElementNamespace.Html;

    /// <summary>The interpolation open delimiter. Defaults to <c>{{</c>.</summary>
    public string DelimiterOpen { get; set; } = "{{";

    /// <summary>The interpolation close delimiter. Defaults to <c>}}</c>.</summary>
    public string DelimiterClose { get; set; } = "}}";

    /// <summary>The whitespace strategy. Defaults to <see cref="WhitespaceStrategy.Condense"/>.</summary>
    public WhitespaceStrategy Whitespace { get; set; } = WhitespaceStrategy.Condense;

    /// <summary>Whether comment nodes are kept. Defaults to <see langword="true"/>.</summary>
    public bool KeepComments { get; set; } = true;

    /// <summary>Whether a tag is a void element (e.g. <c>&lt;br&gt;</c>). Defaults to never.</summary>
    public Func<string, bool> IsVoidTag { get; set; } = static _ => false;

    /// <summary>Whether a tag preserves whitespace (e.g. <c>&lt;pre&gt;</c>). Defaults to never.</summary>
    public Func<string, bool> IsPreTag { get; set; } = static _ => false;

    /// <summary>Whether a tag ignores its first newline (<c>&lt;pre&gt;</c>/<c>&lt;textarea&gt;</c>). Defaults to never.</summary>
    public Func<string, bool> IsIgnoreNewlineTag { get; set; } = static _ => false;

    /// <summary>Whether a tag is a platform-native element. <see langword="null"/> (unknown) by default.</summary>
    public Func<string, bool>? IsNativeTag { get; set; }

    /// <summary>Whether a tag is a custom element (never a component). Defaults to never.</summary>
    public Func<string, bool> IsCustomElement { get; set; } = static _ => false;

    /// <summary>Whether a tag names a built-in component (e.g. <c>Transition</c>). <see langword="null"/> by default.</summary>
    public Func<string, bool>? IsBuiltInComponent { get; set; }

    /// <summary>
    /// Infers a child element's namespace from its tag, the parent element (or <see langword="null"/> at
    /// root), and the root namespace. The base default always returns <see cref="ElementNamespace.Html"/>;
    /// <see cref="CreateHtml"/> installs the WHATWG inference.
    /// </summary>
    public Func<string, ElementNode?, ElementNamespace, ElementNamespace> GetNamespace { get; set; }
        = static (_, _, _) => ElementNamespace.Html;

    /// <summary>Receives recoverable parse errors. Errors are not thrown. <see langword="null"/> by default.</summary>
    public Action<CompilerError>? OnError { get; set; }

    /// <summary>
    /// Creates HTML-mode options mirroring <c>@vue/compiler-dom</c>'s <c>parserOptions</c>: special
    /// handling for <c>&lt;script&gt;</c>/<c>&lt;style&gt;</c> (raw text) and
    /// <c>&lt;title&gt;</c>/<c>&lt;textarea&gt;</c> (RCDATA), void-tag self-closing, and SVG/MathML
    /// namespace switching. The tag/namespace tables come from the shared DOM knowledge
    /// (<see cref="ElementNamespace"/> inference below reproduces the WHATWG tree-construction dispatcher).
    /// </summary>
    public static ParserOptions CreateHtml() => new()
    {
        Mode = TemplateParseMode.Html,
        IsVoidTag = CompilerDomKnowledge.IsVoidTag,
        IsNativeTag = CompilerDomKnowledge.IsNativeTag,
        IsPreTag = static tag => tag == "pre",
        IsIgnoreNewlineTag = static tag => tag == "pre" || tag == "textarea",
        IsBuiltInComponent = static tag =>
            tag is "Transition" or "transition" or "TransitionGroup" or "transition-group",
        GetNamespace = InferHtmlNamespace,
    };

    // Port of @vue/compiler-dom's getNamespace (WHATWG tree-construction dispatcher:
    // https://html.spec.whatwg.org/multipage/parsing.html#tree-construction-dispatcher).
    private static ElementNamespace InferHtmlNamespace(string tag, ElementNode? parent, ElementNamespace rootNamespace)
    {
        var inferredNamespace = parent is not null ? parent.Namespace : rootNamespace;
        if (parent is not null && inferredNamespace == ElementNamespace.MathML)
        {
            if (parent.Tag == "annotation-xml")
            {
                if (tag == "svg")
                {
                    return ElementNamespace.Svg;
                }

                if (parent.Properties.Any(property =>
                        property is AttributeNode attribute &&
                        attribute.Name == "encoding" &&
                        attribute.Value is not null &&
                        (attribute.Value.Content == "text/html" ||
                         attribute.Value.Content == "application/xhtml+xml")))
                {
                    inferredNamespace = ElementNamespace.Html;
                }
            }
            else if (IsMathMlTextIntegrationTag(parent.Tag) && tag != "mglyph" && tag != "malignmark")
            {
                inferredNamespace = ElementNamespace.Html;
            }
        }
        else if (parent is not null && inferredNamespace == ElementNamespace.Svg &&
                 (parent.Tag == "foreignObject" || parent.Tag == "desc" || parent.Tag == "title"))
        {
            inferredNamespace = ElementNamespace.Html;
        }

        if (inferredNamespace == ElementNamespace.Html)
        {
            if (tag == "svg")
            {
                return ElementNamespace.Svg;
            }

            if (tag == "math")
            {
                return ElementNamespace.MathML;
            }
        }

        return inferredNamespace;
    }

    // Upstream /^m(?:[ions]|text)$/: the MathML text integration points.
    private static bool IsMathMlTextIntegrationTag(string tag)
        => tag is "mi" or "mo" or "mn" or "ms" or "mtext";

    /// <summary>
    /// Shallow-copies the options so <see cref="TemplateSyntaxParser"/> can intercept
    /// <see cref="OnError"/> per parse without mutating the caller's instance.
    /// </summary>
    internal ParserOptions Clone() => (ParserOptions)MemberwiseClone();
}
