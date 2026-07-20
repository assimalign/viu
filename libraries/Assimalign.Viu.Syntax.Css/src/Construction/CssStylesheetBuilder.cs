using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// A small fluent accumulator over <see cref="CssSyntaxFactory"/> for assembling a stylesheet's top-level
/// rules incrementally, then materializing them into an immutable <see cref="CssStylesheetNode"/>. Rules
/// serialize in the exact order they are added — the builder never reorders — so a consumer that needs a
/// canonical ordering (such as the build-time utility-first CSS engine [V01.01.12.16]) adds its rules in
/// that order.
/// </summary>
/// <remarks>
/// The builder is a transient, single-threaded construction helper; only its <see cref="Build"/> output —
/// an immutable value-equatable record graph — participates in equality and the incremental-generator
/// cache. <see cref="Build"/> snapshots the accumulated rules, so it may be called more than once and the
/// builder may be reused afterward without the earlier result changing.
/// </remarks>
public sealed class CssStylesheetBuilder
{
    private readonly List<CssSyntaxNode> rules = new();

    /// <summary>The number of top-level rules accumulated so far.</summary>
    public int Count => rules.Count;

    /// <summary>Appends a top-level rule (a qualified rule or at-rule).</summary>
    /// <param name="rule">The rule to append.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
    public CssStylesheetBuilder Add(CssSyntaxNode rule)
    {
        if (rule is null)
        {
            throw new ArgumentNullException(nameof(rule));
        }

        rules.Add(rule);
        return this;
    }

    /// <summary>Appends a sequence of top-level rules, in order.</summary>
    /// <param name="rulesToAdd">The rules to append.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rulesToAdd"/> or any element is <see langword="null"/>.</exception>
    public CssStylesheetBuilder AddRange(IEnumerable<CssSyntaxNode> rulesToAdd)
    {
        if (rulesToAdd is null)
        {
            throw new ArgumentNullException(nameof(rulesToAdd));
        }

        foreach (var rule in rulesToAdd)
        {
            Add(rule);
        }

        return this;
    }

    /// <summary>Materializes the accumulated rules into an immutable stylesheet, snapshotting the current contents.</summary>
    /// <returns>The constructed stylesheet.</returns>
    public CssStylesheetNode Build() => CssSyntaxFactory.Stylesheet(rules);
}
