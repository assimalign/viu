using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// Constructs <see cref="CssStylesheetNode"/> graphs — qualified rules, declarations, conditional-group
/// at-rules (<c>@media</c> and friends), and the selector model — <b>from code</b>, as the same
/// record-graph node types the <see cref="CssSyntaxParser"/> produces, following the CSS Syntax Module
/// Level 3 parser output model (https://www.w3.org/TR/css-syntax-3/#parsing). The parser turns text into
/// that graph; this factory builds the identical graph without any text, so a downstream generator (the
/// build-time utility-first CSS engine [V01.01.12.16]) can synthesize rules from scratch and hand them to
/// the same deterministic canonical serializer (<see cref="CssStylesheetWriter"/> /
/// <see cref="CssScopedRewriter"/>).
/// </summary>
/// <remarks>
/// <para>
/// Every node this factory mints carries a synthetic <see cref="SourceLocation"/> (see
/// <see cref="CssSyntheticLocation"/>): constructed nodes have no source span, the base cluster's
/// exact-slice invariant does not apply to them, and they are explicitly marked so. Construction is
/// otherwise value-pure — the factory copies every incoming collection into an owned array so the returned
/// record graph is immutable and its value equality is stable, and it produces no lazy or shared mutable
/// state. Two graphs built from equal inputs are equal and hash equally; serialization order is exactly the
/// order of the lists passed in (the factory never reorders — a consumer that needs a canonical ordering
/// sorts before constructing).
/// </para>
/// <para>
/// The surface is language-agnostic generic CSS construction: it knows nothing about utilities, variants,
/// or themes. It builds ordinary selectors and Vue's reserved functional pseudos (<c>:deep()</c>,
/// <c>:slotted()</c>, <c>:global()</c>) — which only the parser produces, and which the scoped rewrite,
/// not construction, consumes — are intentionally out of scope; the factory builds ordinary pseudos only.
/// Invalid arguments throw <see cref="ArgumentNullException"/>, matching the serializer entry points; the
/// recoverable never-throw contract applies to <em>parsing</em>, not to programmatic misuse.
/// </para>
/// </remarks>
public static class CssSyntaxFactory
{
    /// <summary>
    /// Builds a declaration — a property/value pair, the CSS Syntax Level 3 <c>&lt;declaration&gt;</c>
    /// (https://www.w3.org/TR/css-syntax-3/#declaration).
    /// </summary>
    /// <param name="property">The property name (e.g. <c>color</c>, <c>background-color</c>).</param>
    /// <param name="value">The declaration value, excluding any <c>!important</c> flag (emitted verbatim).</param>
    /// <param name="important">Whether the declaration carries <c>!important</c>.</param>
    /// <returns>The constructed declaration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    public static CssDeclarationNode Declaration(string property, string value, bool important = false)
    {
        if (property is null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return new CssDeclarationNode
        {
            Property = property,
            Value = value,
            Important = important,
            Location = CssSyntheticLocation.Create(),
        };
    }

    /// <summary>
    /// Builds a simple selector part — a type, universal, class, id, or attribute selector, per the W3C
    /// Selectors Level 4 simple selectors (https://www.w3.org/TR/selectors-4/#simple).
    /// </summary>
    /// <param name="kind">The simple-selector kind.</param>
    /// <param name="text">The exact selector text the serializer emits verbatim (e.g. <c>.foo</c>, <c>#bar</c>, <c>div</c>, <c>*</c>, <c>[type="text"]</c>).</param>
    /// <returns>The constructed simple selector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static CssSimpleSelectorNode SimpleSelector(CssSimpleSelectorKind kind, string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        return new CssSimpleSelectorNode
        {
            Selector = kind,
            Text = text,
            // A simple selector's own text is what the serializer emits; mirror the parser, whose
            // Location.Source equals the selector text, so the synthetic node is self-consistent.
            Location = CssSyntheticLocation.Create(text),
        };
    }

    /// <summary>
    /// Builds a combinator part between two compound selectors, per the W3C Selectors Level 4 combinators
    /// (https://www.w3.org/TR/selectors-4/#combinators). The serializer renders a combinator from its
    /// <paramref name="combinator"/> kind, so the synthetic location carries no text.
    /// </summary>
    /// <param name="combinator">The combinator kind (descendant, child, next-sibling, subsequent-sibling).</param>
    /// <returns>The constructed combinator.</returns>
    public static CssCombinatorNode Combinator(CssCombinatorKind combinator)
        => new()
        {
            Combinator = combinator,
            Location = CssSyntheticLocation.Create(),
        };

    /// <summary>
    /// Builds an ordinary pseudo-class or pseudo-element selector part (e.g. <c>:hover</c>, <c>::before</c>).
    /// The serializer reads a pseudo's text back from its <see cref="SourceLocation.Source"/>, so the
    /// synthetic location carries the leading-colon form (<c>:name</c> or <c>::name</c>).
    /// </summary>
    /// <param name="name">The pseudo name without its leading colon(s) (e.g. <c>hover</c>, <c>before</c>).</param>
    /// <param name="isElement">Whether to write the double-colon pseudo-element form (<c>::name</c>).</param>
    /// <returns>The constructed pseudo selector (always <see cref="CssPseudoSelectorKind.Normal"/>; reserved functional pseudos are parser-only).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static CssPseudoSelectorNode Pseudo(string name, bool isElement = false)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        var text = (isElement ? "::" : ":") + name;
        return new CssPseudoSelectorNode
        {
            Pseudo = CssPseudoSelectorKind.Normal,
            Name = name,
            IsElement = isElement,
            Argument = null,
            Location = CssSyntheticLocation.Create(text),
        };
    }

    /// <summary>
    /// Builds a complex selector — a flat, source-order sequence of parts (simple selectors, pseudo
    /// selectors, and the combinators between compounds), the W3C Selectors Level 4
    /// <c>&lt;complex-selector&gt;</c> (https://www.w3.org/TR/selectors-4/#typedef-complex-selector).
    /// </summary>
    /// <param name="parts">The selector parts, in the order they should serialize.</param>
    /// <returns>The constructed complex selector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parts"/> or any element is <see langword="null"/>.</exception>
    public static CssComplexSelectorNode ComplexSelector(IReadOnlyList<CssSelectorPartNode> parts)
        => new()
        {
            Parts = ToOwnedList(parts, nameof(parts)),
            Location = CssSyntheticLocation.Create(),
        };

    /// <summary>
    /// Builds a selector list — the comma-separated complex selectors of a rule prelude, the W3C Selectors
    /// Level 4 <c>&lt;complex-selector-list&gt;</c> (https://www.w3.org/TR/selectors-4/#typedef-complex-selector-list).
    /// </summary>
    /// <param name="selectors">The complex selectors, in the order they should serialize.</param>
    /// <returns>The constructed selector list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selectors"/> or any element is <see langword="null"/>.</exception>
    public static CssSelectorListNode SelectorList(IReadOnlyList<CssComplexSelectorNode> selectors)
        => new()
        {
            Selectors = ToOwnedList(selectors, nameof(selectors)),
            Location = CssSyntheticLocation.Create(),
        };

    /// <summary>
    /// Builds a qualified rule — a selector prelude and a declaration block, the CSS Syntax Level 3
    /// <c>&lt;qualified-rule&gt;</c> (https://www.w3.org/TR/css-syntax-3/#qualified-rule). The rule's
    /// <see cref="CssQualifiedRuleNode.Prelude"/> is rendered from <paramref name="selectors"/> so it stays
    /// consistent with the parsed parts the serializer emits.
    /// </summary>
    /// <param name="selectors">The rule's selector list.</param>
    /// <param name="declarations">The rule's declarations, in the order they should serialize.</param>
    /// <returns>The constructed qualified rule.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selectors"/>, <paramref name="declarations"/>, or any declaration is <see langword="null"/>.</exception>
    public static CssQualifiedRuleNode QualifiedRule(CssSelectorListNode selectors, IReadOnlyList<CssDeclarationNode> declarations)
    {
        if (selectors is null)
        {
            throw new ArgumentNullException(nameof(selectors));
        }

        return new CssQualifiedRuleNode
        {
            Prelude = CssSelectorWriter.Render(selectors),
            Selectors = selectors,
            Declarations = ToOwnedList(declarations, nameof(declarations)),
            Location = CssSyntheticLocation.Create(),
        };
    }

    /// <summary>
    /// Builds a qualified rule from a single complex selector — a convenience over
    /// <see cref="QualifiedRule(CssSelectorListNode, IReadOnlyList{CssDeclarationNode})"/> that wraps
    /// <paramref name="selector"/> in a one-element selector list.
    /// </summary>
    /// <param name="selector">The rule's single complex selector.</param>
    /// <param name="declarations">The rule's declarations, in the order they should serialize.</param>
    /// <returns>The constructed qualified rule.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/>, <paramref name="declarations"/>, or any declaration is <see langword="null"/>.</exception>
    public static CssQualifiedRuleNode QualifiedRule(CssComplexSelectorNode selector, IReadOnlyList<CssDeclarationNode> declarations)
    {
        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return QualifiedRule(SelectorList(new[] { selector }), declarations);
    }

    /// <summary>
    /// Builds a conditional-group at-rule — a <c>{ … }</c> block at-rule whose body is a list of nested
    /// rules, the CSS Conditional Rules <c>&lt;conditional-group-rule&gt;</c>
    /// (https://www.w3.org/TR/css-conditional-3/). Use for <c>@media</c>, <c>@supports</c>,
    /// <c>@container</c>, and similar.
    /// </summary>
    /// <param name="name">The at-keyword without its leading <c>@</c> (e.g. <c>media</c>, <c>supports</c>).</param>
    /// <param name="prelude">The condition text between the name and the block (e.g. <c>(min-width: 768px)</c>), or empty.</param>
    /// <param name="body">The nested rules, in the order they should serialize.</param>
    /// <returns>The constructed at-rule with <see cref="CssAtRuleNode.HasBlock"/> set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="prelude"/>, <paramref name="body"/>, or any body element is <see langword="null"/>.</exception>
    public static CssAtRuleNode ConditionalGroupAtRule(string name, string prelude, IReadOnlyList<CssSyntaxNode> body)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (prelude is null)
        {
            throw new ArgumentNullException(nameof(prelude));
        }

        return new CssAtRuleNode
        {
            Name = name,
            Prelude = prelude,
            HasBlock = true,
            Body = ToOwnedList(body, nameof(body)),
            Location = CssSyntheticLocation.Create(),
        };
    }

    /// <summary>
    /// Builds a <c>@media</c> conditional-group at-rule — a convenience over
    /// <see cref="ConditionalGroupAtRule(string, string, IReadOnlyList{CssSyntaxNode})"/> with the name
    /// <c>media</c> (https://www.w3.org/TR/css-conditional-3/#at-media).
    /// </summary>
    /// <param name="condition">The media condition (e.g. <c>(min-width: 768px)</c>).</param>
    /// <param name="body">The nested rules, in the order they should serialize.</param>
    /// <returns>The constructed <c>@media</c> rule.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/>, <paramref name="body"/>, or any body element is <see langword="null"/>.</exception>
    public static CssAtRuleNode Media(string condition, IReadOnlyList<CssSyntaxNode> body)
        => ConditionalGroupAtRule("media", condition, body);

    /// <summary>
    /// Builds the stylesheet root — the CSS Syntax Level 3 top-level list of rules
    /// (https://www.w3.org/TR/css-syntax-3/#parse-stylesheet).
    /// </summary>
    /// <param name="rules">The top-level rules, in the order they should serialize.</param>
    /// <returns>The constructed stylesheet.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> or any element is <see langword="null"/>.</exception>
    public static CssStylesheetNode Stylesheet(IReadOnlyList<CssSyntaxNode> rules)
        => new()
        {
            Rules = ToOwnedList(rules, nameof(rules)),
            Location = CssSyntheticLocation.Create(),
        };

    // Copies an incoming collection into a fresh owned array wrapped as a SyntaxList<T>, so the returned
    // node graph is immutable regardless of what the caller later does with its list, and rejects null
    // elements up front rather than letting a null child silently corrupt serialization or hashing.
    private static SyntaxList<T> ToOwnedList<T>(IReadOnlyList<T> items, string parameterName) where T : class
    {
        if (items is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (items.Count == 0)
        {
            return SyntaxList<T>.Empty;
        }

        var owned = new T[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            owned[index] = items[index] ?? throw new ArgumentException("The collection must not contain a null element.", parameterName);
        }

        return new SyntaxList<T>(owned);
    }
}
