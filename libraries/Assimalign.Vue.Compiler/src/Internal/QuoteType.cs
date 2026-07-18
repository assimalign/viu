namespace Assimalign.Vue.Compiler;

/// <summary>
/// How an attribute value was quoted, reported to <see cref="ITokenizerCallbacks.OnAttributeEnd"/>.
/// The C# port of Vue 3.5's <c>QuoteType</c> (<c>@vue/compiler-core</c> <c>tokenizer.ts</c>).
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
