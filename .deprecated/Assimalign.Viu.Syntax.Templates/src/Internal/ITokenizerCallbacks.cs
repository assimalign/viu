namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The sink the <see cref="Tokenizer"/> emits token events to, implemented by the parser. The C# port
/// of Vue 3.5's <c>Callbacks</c> interface (<c>@vue/compiler-core</c> <c>tokenizer.ts</c>). All ranges
/// are half-open <c>[start, end)</c> offsets into the source buffer.
/// </summary>
/// <remarks>
/// An interface (rather than the repo's hot-path abstract-base-class guidance) because the parser is the
/// callback target and must be its own type; template parsing runs at build time, not on the runtime
/// render hot path.
/// </remarks>
internal interface ITokenizerCallbacks
{
    /// <summary>The number of currently open elements (for single-file-component root detection).</summary>
    int OpenElementCount { get; }

    /// <summary>Emitted for a run of character data.</summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnText(int start, int end);

    /// <summary>Emitted for a complete interpolation including its delimiters.</summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnInterpolation(int start, int end);

    /// <summary>Emitted for an open tag name.</summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnOpenTagName(int start, int end);

    /// <summary>Emitted at the <c>&gt;</c> that ends an open tag.</summary>
    /// <param name="end">The offset of the <c>&gt;</c>.</param>
    void OnOpenTagEnd(int end);

    /// <summary>Emitted at the <c>&gt;</c> that ends a self-closing tag.</summary>
    /// <param name="end">The offset of the <c>&gt;</c>.</param>
    void OnSelfClosingTag(int end);

    /// <summary>Emitted for a close tag name.</summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnCloseTag(int start, int end);

    /// <summary>Emitted for a run of attribute-value data.</summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnAttributeData(int start, int end);

    /// <summary>Emitted at the end of an attribute (value or bare name).</summary>
    /// <param name="quote">How the value was quoted.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnAttributeEnd(QuoteType quote, int end);

    /// <summary>Emitted for a plain attribute name.</summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnAttributeName(int start, int end);

    /// <summary>Emitted just after an attribute name ends.</summary>
    /// <param name="end">The exclusive end offset.</param>
    void OnAttributeNameEnd(int end);

    /// <summary>Emitted for a directive name segment.</summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnDirectiveName(int start, int end);

    /// <summary>Emitted for a directive argument segment.</summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnDirectiveArgument(int start, int end);

    /// <summary>Emitted for a directive modifier segment.</summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnDirectiveModifier(int start, int end);

    /// <summary>Emitted for a comment body (excluding delimiters).</summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnComment(int start, int end);

    /// <summary>Emitted for a CDATA body (excluding delimiters).</summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnCdata(int start, int end);

    /// <summary>Emitted for a processing instruction body.</summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    void OnProcessingInstruction(int start, int end);

    /// <summary>Emitted once when the input is exhausted.</summary>
    void OnEnd();

    /// <summary>Emitted for a recoverable parse error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="index">The offset the error points at.</param>
    void OnError(CompilerErrorCode code, int index);
}
