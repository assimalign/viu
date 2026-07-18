using System;
using System.Collections.Generic;

namespace Assimalign.Vue.Syntax;

/// <summary>
/// Configures an <see cref="AggregateSyntaxParser{T}"/>: everything a
/// <see cref="SyntaxParserOptions{T}"/> carries, plus the parser registrations that route each
/// container node's embedded <see cref="SyntaxSource"/> to the language parser that understands it.
/// Registration order is significant — the first registration whose predicate matches wins, so more
/// specific predicates (e.g. <c>lang</c>-qualified) register before general ones.
/// </summary>
/// <typeparam name="T">The container's node type — the unit an embedded source is attached to.</typeparam>
public class AggregateSyntaxParserOptions<T> : SyntaxParserOptions<T> where T : SyntaxNode
{
    private readonly List<AggregateSyntaxParserRegistration> registrations = new List<AggregateSyntaxParserRegistration>();

    /// <summary>The parser registrations, in registration order.</summary>
    public IReadOnlyList<AggregateSyntaxParserRegistration> Registrations => registrations;

    /// <summary>
    /// Registers <paramref name="parser"/> for every embedded source <paramref name="predicate"/>
    /// matches — the incremental-generator-style seam build tooling uses to attach a language parser
    /// to a block name, <c>lang</c> option, or file type without the container library referencing it.
    /// </summary>
    /// <param name="predicate">Selects the sources the parser understands.</param>
    /// <param name="parser">The parser to dispatch matching sources to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> or <paramref name="parser"/> is <see langword="null"/>.</exception>
    public void RegisterParser(SyntaxSourcePredicate predicate, SyntaxParser parser)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        if (parser is null)
        {
            throw new ArgumentNullException(nameof(parser));
        }

        registrations.Add(new AggregateSyntaxParserRegistration(predicate, parser));
    }
}
