namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// How an attribute value was quoted, reported to <see cref="ITokenizerCallbacks.OnAttributeEnd"/>.
/// </summary>
internal enum QuoteType
{
    /// <summary>The attribute had no value.</summary>
    NoValue = 0,

    /// <summary>The value was unquoted.</summary>
    Unquoted = 1,

    /// <summary>The value was single-quoted.</summary>
    Single = 2,

    /// <summary>The value was double-quoted.</summary>
    Double = 3,
}
