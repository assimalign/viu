using System.Collections.Generic;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// The WHATWG-derived HTML tokenizer, driving character data, tags, attributes, directives, comments,
/// CDATA, raw-text/RCDATA content, and Vue interpolation delimiters. The C# port of Vue 3.5's
/// <c>Tokenizer</c> (<c>@vue/compiler-core</c> <c>tokenizer.ts</c>, itself adapted from
/// <c>htmlparser2</c>). Scanning is index-based over the source string — allocation-free per character,
/// no regex — matching upstream's <c>charCodeAt</c> loop. Character references are decoded later by the
/// parser (see <see cref="HtmlEntityDecoder"/>), so the <c>InEntity</c> state and its callbacks are
/// intentionally omitted; the observable AST is unchanged.
/// </summary>
/// <remarks>Single-threaded, single-use per <see cref="Parse"/> call. Reference:
/// https://html.spec.whatwg.org/multipage/parsing.html#tokenization.</remarks>
internal sealed class Tokenizer
{
    private static readonly char[] DefaultDelimiterOpen = "{{".ToCharArray();
    private static readonly char[] DefaultDelimiterClose = "}}".ToCharArray();

    private readonly ITokenizerCallbacks callbacks;
    private readonly List<int> newlines = new();

    private string buffer = string.Empty;
    private int index;
    private int sectionStart;
    private int sequenceIndex;
    private int delimiterIndex = -1;

    /// <summary>Creates a tokenizer emitting to <paramref name="callbacks"/>.</summary>
    /// <param name="callbacks">The token event sink (the parser).</param>
    public Tokenizer(ITokenizerCallbacks callbacks) => this.callbacks = callbacks;

    /// <summary>The current tokenizer state.</summary>
    public TokenizerState State { get; set; } = TokenizerState.Text;

    /// <summary>The offset where the section currently being read began.</summary>
    public int SectionStart => sectionStart;

    /// <summary>The interpolation open delimiter (default <c>{{</c>).</summary>
    public char[] DelimiterOpen { get; set; } = DefaultDelimiterOpen;

    /// <summary>The interpolation close delimiter (default <c>}}</c>).</summary>
    public char[] DelimiterClose { get; set; } = DefaultDelimiterClose;

    /// <summary>The end sequence being matched inside RCDATA/RAWTEXT or a comment/CDATA body.</summary>
    public char[] CurrentSequence { get; private set; } = System.Array.Empty<char>();

    /// <summary>Whether the tokenizer is inside raw-text/RCDATA content.</summary>
    public bool InRcdata { get; set; }

    /// <summary>Whether RCDATA handling is disabled (foreign SVG/MathML content).</summary>
    public bool InXml { get; set; }

    /// <summary>Whether interpolation parsing is disabled (inside a <c>v-pre</c> boundary).</summary>
    public bool InVPre { get; set; }

    /// <summary>The parse mode selecting special-element behaviour.</summary>
    public TemplateParseMode Mode { get; set; } = TemplateParseMode.Base;

    /// <summary>Whether the tokenizer is at the root level of a single-file component.</summary>
    public bool InSfcRoot => Mode == TemplateParseMode.Sfc && callbacks.OpenElementCount == 0;

    /// <summary>Resets all state so the instance can parse again.</summary>
    public void Reset()
    {
        State = TokenizerState.Text;
        Mode = TemplateParseMode.Base;
        buffer = string.Empty;
        sectionStart = 0;
        index = 0;
        InRcdata = false;
        InXml = false;
        InVPre = false;
        CurrentSequence = System.Array.Empty<char>();
        sequenceIndex = 0;
        delimiterIndex = -1;
        newlines.Clear();
        DelimiterOpen = DefaultDelimiterOpen;
        DelimiterClose = DefaultDelimiterClose;
    }

    /// <summary>
    /// Builds a <see cref="Position"/> for <paramref name="offset"/> using recorded newline positions.
    /// The offset must already have been processed so its newlines are recorded.
    /// </summary>
    /// <param name="offset">The character offset.</param>
    public Position GetPosition(int offset)
    {
        var line = 1;
        var column = offset + 1;
        for (var i = newlines.Count - 1; i >= 0; i--)
        {
            var newlineIndex = newlines[i];
            if (offset > newlineIndex)
            {
                line = i + 2;
                column = offset - newlineIndex;
                break;
            }
        }

        return new Position(offset, line, column);
    }

    /// <summary>Enters raw-text/RCDATA mode looking for <paramref name="sequence"/>.</summary>
    /// <param name="sequence">The end sequence to scan for.</param>
    /// <param name="offset">The starting match offset within the sequence.</param>
    public void EnterRcdata(char[] sequence, int offset)
    {
        InRcdata = true;
        CurrentSequence = sequence;
        sequenceIndex = offset;
    }

    /// <summary>Tokenizes <paramref name="input"/>, emitting events to the callbacks.</summary>
    /// <param name="input">The template source.</param>
    public void Parse(string input)
    {
        buffer = input;
        while (index < buffer.Length)
        {
            var character = buffer[index];
            if (character == '\n')
            {
                newlines.Add(index);
            }

            switch (State)
            {
                case TokenizerState.Text:
                    StateText(character);
                    break;
                case TokenizerState.InterpolationOpen:
                    StateInterpolationOpen(character);
                    break;
                case TokenizerState.Interpolation:
                    StateInterpolation(character);
                    break;
                case TokenizerState.InterpolationClose:
                    StateInterpolationClose(character);
                    break;
                case TokenizerState.SpecialStartSequence:
                    StateSpecialStartSequence(character);
                    break;
                case TokenizerState.InRcdata:
                    StateInRcdata(character);
                    break;
                case TokenizerState.CdataSequence:
                    StateCdataSequence(character);
                    break;
                case TokenizerState.InAttributeValueDoubleQuote:
                    HandleInAttributeValue(character, '"');
                    break;
                case TokenizerState.InAttributeName:
                    StateInAttributeName(character);
                    break;
                case TokenizerState.InDirectiveName:
                    StateInDirectiveName(character);
                    break;
                case TokenizerState.InDirectiveArgument:
                    StateInDirectiveArgument(character);
                    break;
                case TokenizerState.InDirectiveDynamicArgument:
                    StateInDynamicDirectiveArgument(character);
                    break;
                case TokenizerState.InDirectiveModifier:
                    StateInDirectiveModifier(character);
                    break;
                case TokenizerState.InCommentLike:
                    StateInCommentLike(character);
                    break;
                case TokenizerState.InSpecialComment:
                    StateInSpecialComment(character);
                    break;
                case TokenizerState.BeforeAttributeName:
                    StateBeforeAttributeName(character);
                    break;
                case TokenizerState.InTagName:
                    StateInTagName(character);
                    break;
                case TokenizerState.InSfcRootTagName:
                    StateInSfcRootTagName(character);
                    break;
                case TokenizerState.InClosingTagName:
                    StateInClosingTagName(character);
                    break;
                case TokenizerState.BeforeTagName:
                    StateBeforeTagName(character);
                    break;
                case TokenizerState.AfterAttributeName:
                    StateAfterAttributeName(character);
                    break;
                case TokenizerState.InAttributeValueSingleQuote:
                    HandleInAttributeValue(character, '\'');
                    break;
                case TokenizerState.BeforeAttributeValue:
                    StateBeforeAttributeValue(character);
                    break;
                case TokenizerState.BeforeClosingTagName:
                    StateBeforeClosingTagName(character);
                    break;
                case TokenizerState.AfterClosingTagName:
                    StateAfterClosingTagName(character);
                    break;
                case TokenizerState.BeforeSpecialS:
                    StateBeforeSpecialS(character);
                    break;
                case TokenizerState.BeforeSpecialT:
                    StateBeforeSpecialT(character);
                    break;
                case TokenizerState.InAttributeValueNoQuote:
                    StateInAttributeValueNoQuotes(character);
                    break;
                case TokenizerState.InSelfClosingTag:
                    StateInSelfClosingTag(character);
                    break;
                case TokenizerState.InDeclaration:
                    StateInDeclaration(character);
                    break;
                case TokenizerState.BeforeDeclaration:
                    StateBeforeDeclaration(character);
                    break;
                case TokenizerState.BeforeComment:
                    StateBeforeComment(character);
                    break;
                case TokenizerState.InProcessingInstruction:
                    StateInProcessingInstruction(character);
                    break;
            }

            index++;
        }

        Cleanup();
        Finish();
    }

    private static bool IsWhitespace(char character) => character == ' ' || character == '\n' || character == '\t' || character == '\f' || character == '\r';

    private static bool IsTagStartChar(char character) => (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z');

    private static bool IsEndOfTagSection(char character) => character == '/' || character == '>' || IsWhitespace(character);

    private int Peek() => index + 1 < buffer.Length ? buffer[index + 1] : -1;

    private void StateText(char character)
    {
        if (character == '<')
        {
            if (index > sectionStart)
            {
                callbacks.OnText(sectionStart, index);
            }

            State = TokenizerState.BeforeTagName;
            sectionStart = index;
        }
        else if (!InVPre && character == DelimiterOpen[0])
        {
            State = TokenizerState.InterpolationOpen;
            delimiterIndex = 0;
            StateInterpolationOpen(character);
        }
    }

    private void StateInterpolationOpen(char character)
    {
        if (character == DelimiterOpen[delimiterIndex])
        {
            if (delimiterIndex == DelimiterOpen.Length - 1)
            {
                var start = index + 1 - DelimiterOpen.Length;
                if (start > sectionStart)
                {
                    callbacks.OnText(sectionStart, start);
                }

                State = TokenizerState.Interpolation;
                sectionStart = start;
            }
            else
            {
                delimiterIndex++;
            }
        }
        else if (InRcdata)
        {
            State = TokenizerState.InRcdata;
            StateInRcdata(character);
        }
        else
        {
            State = TokenizerState.Text;
            StateText(character);
        }
    }

    private void StateInterpolation(char character)
    {
        if (character == DelimiterClose[0])
        {
            State = TokenizerState.InterpolationClose;
            delimiterIndex = 0;
            StateInterpolationClose(character);
        }
    }

    private void StateInterpolationClose(char character)
    {
        if (character == DelimiterClose[delimiterIndex])
        {
            if (delimiterIndex == DelimiterClose.Length - 1)
            {
                callbacks.OnInterpolation(sectionStart, index + 1);
                State = InRcdata ? TokenizerState.InRcdata : TokenizerState.Text;
                sectionStart = index + 1;
            }
            else
            {
                delimiterIndex++;
            }
        }
        else
        {
            State = TokenizerState.Interpolation;
            StateInterpolation(character);
        }
    }

    private void StateSpecialStartSequence(char character)
    {
        var isEnd = sequenceIndex == CurrentSequence.Length;
        var isMatch = isEnd
            ? IsEndOfTagSection(character)
            : (character | 0x20) == CurrentSequence[sequenceIndex];

        if (!isMatch)
        {
            InRcdata = false;
        }
        else if (!isEnd)
        {
            sequenceIndex++;
            return;
        }

        sequenceIndex = 0;
        State = TokenizerState.InTagName;
        StateInTagName(character);
    }

    private void StateInRcdata(char character)
    {
        if (sequenceIndex == CurrentSequence.Length)
        {
            if (character == '>' || IsWhitespace(character))
            {
                var endOfText = index - CurrentSequence.Length;
                if (sectionStart < endOfText)
                {
                    // Spoof the index so reported locations line up with the text end.
                    var actualIndex = index;
                    index = endOfText;
                    callbacks.OnText(sectionStart, endOfText);
                    index = actualIndex;
                }

                sectionStart = endOfText + 2; // Skip over the "</".
                StateInClosingTagName(character);
                InRcdata = false;
                return;
            }

            sequenceIndex = 0;
        }

        if ((character | 0x20) == CurrentSequence[sequenceIndex])
        {
            sequenceIndex += 1;
        }
        else if (sequenceIndex == 0)
        {
            if (CurrentSequence == TokenizerSequences.TitleEnd ||
                (CurrentSequence == TokenizerSequences.TextareaEnd && !InSfcRoot))
            {
                // Title/textarea are RCDATA: interpolation is still parsed (entities decoded later).
                if (!InVPre && character == DelimiterOpen[0])
                {
                    State = TokenizerState.InterpolationOpen;
                    delimiterIndex = 0;
                    StateInterpolationOpen(character);
                }
            }
            else if (FastForwardTo('<'))
            {
                sequenceIndex = 1;
            }
        }
        else
        {
            // On "<", set the sequence index to 1; useful for e.g. "<</script>".
            sequenceIndex = character == '<' ? 1 : 0;
        }
    }

    private void StateCdataSequence(char character)
    {
        if (character == TokenizerSequences.Cdata[sequenceIndex])
        {
            if (++sequenceIndex == TokenizerSequences.Cdata.Length)
            {
                State = TokenizerState.InCommentLike;
                CurrentSequence = TokenizerSequences.CdataEnd;
                sequenceIndex = 0;
                sectionStart = index + 1;
            }
        }
        else
        {
            sequenceIndex = 0;
            State = TokenizerState.InDeclaration;
            StateInDeclaration(character); // Reconsume the character.
        }
    }

    private bool FastForwardTo(char character)
    {
        while (++index < buffer.Length)
        {
            var currentCharacter = buffer[index];
            if (currentCharacter == '\n')
            {
                newlines.Add(index);
            }

            if (currentCharacter == character)
            {
                return true;
            }
        }

        // The parse loop increments index at the end, so leave it at buffer.Length - 1.
        index = buffer.Length - 1;
        return false;
    }

    private void StateInCommentLike(char character)
    {
        if (character == CurrentSequence[sequenceIndex])
        {
            if (++sequenceIndex == CurrentSequence.Length)
            {
                if (CurrentSequence == TokenizerSequences.CdataEnd)
                {
                    callbacks.OnCdata(sectionStart, index - 2);
                }
                else
                {
                    callbacks.OnComment(sectionStart, index - 2);
                }

                sequenceIndex = 0;
                sectionStart = index + 1;
                State = TokenizerState.Text;
            }
        }
        else if (sequenceIndex == 0)
        {
            if (FastForwardTo(CurrentSequence[0]))
            {
                sequenceIndex = 1;
            }
        }
        else if (character != CurrentSequence[sequenceIndex - 1])
        {
            // Allow long sequences, e.g. ---> or ]]]>.
            sequenceIndex = 0;
        }
    }

    private void StartSpecial(char[] sequence, int offset)
    {
        EnterRcdata(sequence, offset);
        State = TokenizerState.SpecialStartSequence;
    }

    private void StateBeforeTagName(char character)
    {
        if (character == '!')
        {
            State = TokenizerState.BeforeDeclaration;
            sectionStart = index + 1;
        }
        else if (character == '?')
        {
            State = TokenizerState.InProcessingInstruction;
            sectionStart = index + 1;
        }
        else if (IsTagStartChar(character))
        {
            sectionStart = index;
            if (Mode == TemplateParseMode.Base)
            {
                State = TokenizerState.InTagName;
            }
            else if (InSfcRoot)
            {
                State = TokenizerState.InSfcRootTagName;
            }
            else if (!InXml)
            {
                if (character == 't')
                {
                    State = TokenizerState.BeforeSpecialT;
                }
                else
                {
                    State = character == 's' ? TokenizerState.BeforeSpecialS : TokenizerState.InTagName;
                }
            }
            else
            {
                State = TokenizerState.InTagName;
            }
        }
        else if (character == '/')
        {
            State = TokenizerState.BeforeClosingTagName;
        }
        else
        {
            State = TokenizerState.Text;
            StateText(character);
        }
    }

    private void StateInTagName(char character)
    {
        if (IsEndOfTagSection(character))
        {
            HandleTagName(character);
        }
    }

    private void StateInSfcRootTagName(char character)
    {
        if (IsEndOfTagSection(character))
        {
            var tag = buffer.Substring(sectionStart, index - sectionStart);
            if (tag != "template")
            {
                EnterRcdata(("</" + tag).ToCharArray(), 0);
            }

            HandleTagName(character);
        }
    }

    private void HandleTagName(char character)
    {
        callbacks.OnOpenTagName(sectionStart, index);
        sectionStart = -1;
        State = TokenizerState.BeforeAttributeName;
        StateBeforeAttributeName(character);
    }

    private void StateBeforeClosingTagName(char character)
    {
        if (IsWhitespace(character))
        {
            // Ignore.
        }
        else if (character == '>')
        {
            callbacks.OnError(CompilerErrorCode.MissingEndTagName, index);
            State = TokenizerState.Text;
            sectionStart = index + 1;
        }
        else
        {
            State = IsTagStartChar(character) ? TokenizerState.InClosingTagName : TokenizerState.InSpecialComment;
            sectionStart = index;
        }
    }

    private void StateInClosingTagName(char character)
    {
        if (character == '>' || IsWhitespace(character))
        {
            callbacks.OnCloseTag(sectionStart, index);
            sectionStart = -1;
            State = TokenizerState.AfterClosingTagName;
            StateAfterClosingTagName(character);
        }
    }

    private void StateAfterClosingTagName(char character)
    {
        if (character == '>')
        {
            State = TokenizerState.Text;
            sectionStart = index + 1;
        }
    }

    private void StateBeforeAttributeName(char character)
    {
        if (character == '>')
        {
            callbacks.OnOpenTagEnd(index);
            State = InRcdata ? TokenizerState.InRcdata : TokenizerState.Text;
            sectionStart = index + 1;
        }
        else if (character == '/')
        {
            State = TokenizerState.InSelfClosingTag;
            if (Peek() != '>')
            {
                callbacks.OnError(CompilerErrorCode.UnexpectedSolidusInTag, index);
            }
        }
        else if (character == '<' && Peek() == '/')
        {
            // Practical handling for "</" appearing in the open-tag state (IDE intermediate input).
            callbacks.OnOpenTagEnd(index);
            State = TokenizerState.BeforeTagName;
            sectionStart = index;
        }
        else if (!IsWhitespace(character))
        {
            if (character == '=')
            {
                callbacks.OnError(CompilerErrorCode.UnexpectedEqualsSignBeforeAttributeName, index);
            }

            HandleAttributeStart(character);
        }
    }

    private void HandleAttributeStart(char character)
    {
        if (character == 'v' && Peek() == '-')
        {
            State = TokenizerState.InDirectiveName;
            sectionStart = index;
        }
        else if (character == '.' || character == ':' || character == '@' || character == '#')
        {
            callbacks.OnDirectiveName(index, index + 1);
            State = TokenizerState.InDirectiveArgument;
            sectionStart = index + 1;
        }
        else
        {
            State = TokenizerState.InAttributeName;
            sectionStart = index;
        }
    }

    private void StateInSelfClosingTag(char character)
    {
        if (character == '>')
        {
            callbacks.OnSelfClosingTag(index);
            State = TokenizerState.Text;
            sectionStart = index + 1;
            InRcdata = false; // Reset special state for self-closing special tags.
        }
        else if (!IsWhitespace(character))
        {
            State = TokenizerState.BeforeAttributeName;
            StateBeforeAttributeName(character);
        }
    }

    private void StateInAttributeName(char character)
    {
        if (character == '=' || IsEndOfTagSection(character))
        {
            callbacks.OnAttributeName(sectionStart, index);
            HandleAttributeNameEnd(character);
        }
        else if (character == '"' || character == '\'' || character == '<')
        {
            callbacks.OnError(CompilerErrorCode.UnexpectedCharacterInAttributeName, index);
        }
    }

    private void StateInDirectiveName(char character)
    {
        if (character == '=' || IsEndOfTagSection(character))
        {
            callbacks.OnDirectiveName(sectionStart, index);
            HandleAttributeNameEnd(character);
        }
        else if (character == ':')
        {
            callbacks.OnDirectiveName(sectionStart, index);
            State = TokenizerState.InDirectiveArgument;
            sectionStart = index + 1;
        }
        else if (character == '.')
        {
            callbacks.OnDirectiveName(sectionStart, index);
            State = TokenizerState.InDirectiveModifier;
            sectionStart = index + 1;
        }
    }

    private void StateInDirectiveArgument(char character)
    {
        if (character == '=' || IsEndOfTagSection(character))
        {
            callbacks.OnDirectiveArgument(sectionStart, index);
            HandleAttributeNameEnd(character);
        }
        else if (character == '[')
        {
            State = TokenizerState.InDirectiveDynamicArgument;
        }
        else if (character == '.')
        {
            callbacks.OnDirectiveArgument(sectionStart, index);
            State = TokenizerState.InDirectiveModifier;
            sectionStart = index + 1;
        }
    }

    private void StateInDynamicDirectiveArgument(char character)
    {
        if (character == ']')
        {
            State = TokenizerState.InDirectiveArgument;
        }
        else if (character == '=' || IsEndOfTagSection(character))
        {
            callbacks.OnDirectiveArgument(sectionStart, index + 1);
            HandleAttributeNameEnd(character);
            callbacks.OnError(CompilerErrorCode.XMissingDynamicDirectiveArgumentEnd, index);
        }
    }

    private void StateInDirectiveModifier(char character)
    {
        if (character == '=' || IsEndOfTagSection(character))
        {
            callbacks.OnDirectiveModifier(sectionStart, index);
            HandleAttributeNameEnd(character);
        }
        else if (character == '.')
        {
            callbacks.OnDirectiveModifier(sectionStart, index);
            sectionStart = index + 1;
        }
    }

    private void HandleAttributeNameEnd(char character)
    {
        sectionStart = index;
        State = TokenizerState.AfterAttributeName;
        callbacks.OnAttributeNameEnd(index);
        StateAfterAttributeName(character);
    }

    private void StateAfterAttributeName(char character)
    {
        if (character == '=')
        {
            State = TokenizerState.BeforeAttributeValue;
        }
        else if (character == '/' || character == '>')
        {
            callbacks.OnAttributeEnd(QuoteType.NoValue, sectionStart);
            sectionStart = -1;
            State = TokenizerState.BeforeAttributeName;
            StateBeforeAttributeName(character);
        }
        else if (!IsWhitespace(character))
        {
            callbacks.OnAttributeEnd(QuoteType.NoValue, sectionStart);
            HandleAttributeStart(character);
        }
    }

    private void StateBeforeAttributeValue(char character)
    {
        if (character == '"')
        {
            State = TokenizerState.InAttributeValueDoubleQuote;
            sectionStart = index + 1;
        }
        else if (character == '\'')
        {
            State = TokenizerState.InAttributeValueSingleQuote;
            sectionStart = index + 1;
        }
        else if (!IsWhitespace(character))
        {
            sectionStart = index;
            State = TokenizerState.InAttributeValueNoQuote;
            StateInAttributeValueNoQuotes(character); // Reconsume token.
        }
    }

    private void HandleInAttributeValue(char character, char quote)
    {
        if (character == quote)
        {
            callbacks.OnAttributeData(sectionStart, index);
            sectionStart = -1;
            callbacks.OnAttributeEnd(quote == '"' ? QuoteType.Double : QuoteType.Single, index + 1);
            State = TokenizerState.BeforeAttributeName;
        }
    }

    private void StateInAttributeValueNoQuotes(char character)
    {
        if (IsWhitespace(character) || character == '>')
        {
            callbacks.OnAttributeData(sectionStart, index);
            sectionStart = -1;
            callbacks.OnAttributeEnd(QuoteType.Unquoted, index);
            State = TokenizerState.BeforeAttributeName;
            StateBeforeAttributeName(character);
        }
        else if (character == '"' || character == '\'' || character == '<' || character == '=' || character == '`')
        {
            callbacks.OnError(CompilerErrorCode.UnexpectedCharacterInUnquotedAttributeValue, index);
        }
    }

    private void StateBeforeDeclaration(char character)
    {
        if (character == '[')
        {
            State = TokenizerState.CdataSequence;
            sequenceIndex = 0;
        }
        else
        {
            State = character == '-' ? TokenizerState.BeforeComment : TokenizerState.InDeclaration;
        }
    }

    private void StateInDeclaration(char character)
    {
        if (character == '>' || FastForwardTo('>'))
        {
            State = TokenizerState.Text;
            sectionStart = index + 1;
        }
    }

    private void StateInProcessingInstruction(char character)
    {
        if (character == '>' || FastForwardTo('>'))
        {
            callbacks.OnProcessingInstruction(sectionStart, index);
            State = TokenizerState.Text;
            sectionStart = index + 1;
        }
    }

    private void StateBeforeComment(char character)
    {
        if (character == '-')
        {
            State = TokenizerState.InCommentLike;
            CurrentSequence = TokenizerSequences.CommentEnd;
            sequenceIndex = 2; // Allow short comments (e.g. <!-->).
            sectionStart = index + 1;
        }
        else
        {
            State = TokenizerState.InDeclaration;
        }
    }

    private void StateInSpecialComment(char character)
    {
        if (character == '>' || FastForwardTo('>'))
        {
            callbacks.OnComment(sectionStart, index);
            State = TokenizerState.Text;
            sectionStart = index + 1;
        }
    }

    private void StateBeforeSpecialS(char character)
    {
        if (character == TokenizerSequences.ScriptEnd[3])
        {
            StartSpecial(TokenizerSequences.ScriptEnd, 4);
        }
        else if (character == TokenizerSequences.StyleEnd[3])
        {
            StartSpecial(TokenizerSequences.StyleEnd, 4);
        }
        else
        {
            State = TokenizerState.InTagName;
            StateInTagName(character); // Consume the token again.
        }
    }

    private void StateBeforeSpecialT(char character)
    {
        if (character == TokenizerSequences.TitleEnd[3])
        {
            StartSpecial(TokenizerSequences.TitleEnd, 4);
        }
        else if (character == TokenizerSequences.TextareaEnd[3])
        {
            StartSpecial(TokenizerSequences.TextareaEnd, 4);
        }
        else
        {
            State = TokenizerState.InTagName;
            StateInTagName(character); // Consume the token again.
        }
    }

    private void Cleanup()
    {
        if (sectionStart != index)
        {
            if (State == TokenizerState.Text ||
                (State == TokenizerState.InRcdata && sequenceIndex == 0))
            {
                callbacks.OnText(sectionStart, index);
                sectionStart = index;
            }
            else if (State == TokenizerState.InAttributeValueDoubleQuote ||
                     State == TokenizerState.InAttributeValueSingleQuote ||
                     State == TokenizerState.InAttributeValueNoQuote)
            {
                callbacks.OnAttributeData(sectionStart, index);
                sectionStart = index;
            }
        }
    }

    private void Finish()
    {
        HandleTrailingData();
        callbacks.OnEnd();
    }

    private void HandleTrailingData()
    {
        var endIndex = buffer.Length;
        if (sectionStart >= endIndex)
        {
            return;
        }

        if (State == TokenizerState.InCommentLike)
        {
            if (CurrentSequence == TokenizerSequences.CdataEnd)
            {
                callbacks.OnCdata(sectionStart, endIndex);
            }
            else
            {
                callbacks.OnComment(sectionStart, endIndex);
            }
        }
        else if (State == TokenizerState.InTagName ||
                 State == TokenizerState.BeforeAttributeName ||
                 State == TokenizerState.BeforeAttributeValue ||
                 State == TokenizerState.AfterAttributeName ||
                 State == TokenizerState.InAttributeName ||
                 State == TokenizerState.InDirectiveName ||
                 State == TokenizerState.InDirectiveArgument ||
                 State == TokenizerState.InDirectiveDynamicArgument ||
                 State == TokenizerState.InDirectiveModifier ||
                 State == TokenizerState.InAttributeValueSingleQuote ||
                 State == TokenizerState.InAttributeValueDoubleQuote ||
                 State == TokenizerState.InAttributeValueNoQuote ||
                 State == TokenizerState.InClosingTagName)
        {
            // Inside an open or closing tag at EOF: not calling the callback signals the tag is ignored.
        }
        else
        {
            callbacks.OnText(sectionStart, endIndex);
        }
    }
}
