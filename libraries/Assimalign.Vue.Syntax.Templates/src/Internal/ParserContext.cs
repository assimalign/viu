using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The stateful parser driver: consumes <see cref="Tokenizer"/> events and builds the located AST. The
/// C# port of Vue 3.5's <c>baseParse</c> and its tokenizer callbacks (<c>@vue/compiler-core</c>
/// <c>parser.ts</c>). One instance parses one template (Vue uses module-level state reset per parse;
/// this port uses a fresh instance instead). Not thread-safe.
/// </summary>
/// <remarks>
/// Divergences from upstream, all AST-preserving: nodes are built into immutable records at close time
/// (Vue mutates in place); character references are decoded when content is materialised (see
/// <see cref="HtmlEntityDecoder"/>); JavaScript expression parsing (<c>prefixIdentifiers</c>, the Babel
/// AST) and <c>v-for</c> expression decomposition are out of [V01.01.05.01] scope. The <c>dirToAttr</c>
/// value location is recomputed so <c>loc.Source</c> matches its offsets for every node (Vue leaves that
/// one source string stale) — the location-accuracy contract of this work item.
/// </remarks>
internal sealed class ParserContext : ITokenizerCallbacks
{
    private static readonly SourceLocation StubLocation =
        new(new Position(0, 1, 1), new Position(0, 1, 1), string.Empty);

    private static readonly HashSet<string> SpecialTemplateDirectives =
        new(StringComparer.Ordinal) { "if", "else", "else-if", "for", "slot" };

    private readonly ParserOptions options;
    private readonly Tokenizer tokenizer;
    private readonly List<ElementBuilder> stack = new();
    private readonly List<TemplateChildNode> rootChildren = new();
    private readonly StringBuilder currentAttributeValue = new();

    private string input = string.Empty;
    private ElementBuilder? currentOpenTag;
    private PropertyBuilder? currentProperty;
    private int currentAttributeStartIndex = -1;
    private int currentAttributeEndIndex = -1;
    private int inPre;
    private bool inVPre;
    private ElementBuilder? currentVPreBoundary;

    /// <summary>Creates a parser driver for the given options.</summary>
    /// <param name="options">The parser options.</param>
    public ParserContext(ParserOptions options)
    {
        this.options = options;
        tokenizer = new Tokenizer(this);
    }

    /// <inheritdoc />
    public int OpenElementCount => stack.Count;

    /// <summary>Parses <paramref name="source"/> into a located AST root.</summary>
    /// <param name="source">The template source.</param>
    public RootNode Parse(string source)
    {
        input = source;
        tokenizer.Mode = options.Mode;
        tokenizer.InXml = options.RootNamespace is ElementNamespace.Svg or ElementNamespace.MathML;
        tokenizer.DelimiterOpen = options.DelimiterOpen.ToCharArray();
        tokenizer.DelimiterClose = options.DelimiterClose.ToCharArray();

        tokenizer.Parse(source);

        var rootLocation = GetLocation(0, source.Length);
        var children = CondenseWhitespace(rootChildren);
        return new RootNode
        {
            Source = source,
            Children = new SyntaxList<TemplateChildNode>(children.ToArray()),
            Location = rootLocation,
        };
    }

    // ---- Tokenizer callbacks ----

    /// <inheritdoc />
    public void OnText(int start, int end)
    {
        var raw = GetSlice(start, end);
        var content = ShouldDecodeText() ? HtmlEntityDecoder.Decode(raw, false) : raw;
        AddText(content, start, end);
    }

    /// <inheritdoc />
    public void OnInterpolation(int start, int end)
    {
        if (inVPre)
        {
            var raw = GetSlice(start, end);
            AddText(ShouldDecodeText() ? HtmlEntityDecoder.Decode(raw, false) : raw, start, end);
            return;
        }

        var innerStart = start + tokenizer.DelimiterOpen.Length;
        var innerEnd = end - tokenizer.DelimiterClose.Length;
        while (innerStart < input.Length && IsWhitespace(input[innerStart]))
        {
            innerStart++;
        }

        while (innerEnd - 1 >= 0 && innerEnd - 1 < input.Length && IsWhitespace(input[innerEnd - 1]))
        {
            innerEnd--;
        }

        var expression = GetSlice(innerStart, innerEnd);
        if (expression.IndexOf('&') >= 0)
        {
            expression = HtmlEntityDecoder.Decode(expression, false);
        }

        AddNode(new InterpolationNode
        {
            Content = CreateSimpleExpression(expression, false, GetLocation(innerStart, innerEnd)),
            Location = GetLocation(start, end),
        });
    }

    /// <inheritdoc />
    public void OnOpenTagName(int start, int end)
    {
        var name = GetSlice(start, end);
        var elementNamespace = options.GetNamespace(name, ParentSnapshot(), options.RootNamespace);
        currentOpenTag = new ElementBuilder(name, elementNamespace, start - 1);
    }

    /// <inheritdoc />
    public void OnOpenTagEnd(int end) => EndOpenTag(end);

    /// <inheritdoc />
    public void OnSelfClosingTag(int end)
    {
        var name = currentOpenTag!.Tag;
        currentOpenTag.IsSelfClosing = true;
        EndOpenTag(end);
        if (stack.Count > 0 && stack[0].Tag == name)
        {
            var element = stack[0];
            stack.RemoveAt(0);
            AddToCurrentChildren(BuildElementNode(element, end, false));
        }
    }

    /// <inheritdoc />
    public void OnCloseTag(int start, int end)
    {
        var name = GetSlice(start, end);
        if (options.IsVoidTag(name))
        {
            return;
        }

        for (var i = 0; i < stack.Count; i++)
        {
            if (!string.Equals(stack[i].Tag, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i > 0)
            {
                EmitError(CompilerErrorCode.XMissingEndTag, stack[0].TagStartOffset);
            }

            for (var j = 0; j <= i; j++)
            {
                var element = stack[0];
                stack.RemoveAt(0);
                AddToCurrentChildren(BuildElementNode(element, end, j < i));
            }

            return;
        }

        EmitError(CompilerErrorCode.XInvalidEndTag, BackTrack(start, '<'));
    }

    /// <inheritdoc />
    public void OnAttributeName(int start, int end) => currentProperty = new PropertyBuilder
    {
        IsDirective = false,
        StartOffset = start,
        Name = GetSlice(start, end),
        NameLocation = GetLocation(start, end),
    };

    /// <inheritdoc />
    public void OnDirectiveName(int start, int end)
    {
        var raw = GetSlice(start, end);
        var name = raw is "." or ":"
            ? "bind"
            : raw == "@"
                ? "on"
                : raw == "#"
                    ? "slot"
                    : raw.Substring(2);

        if (!inVPre && name.Length == 0)
        {
            EmitError(CompilerErrorCode.XMissingDirectiveName, start);
        }

        if (inVPre || name.Length == 0)
        {
            currentProperty = new PropertyBuilder
            {
                IsDirective = false,
                StartOffset = start,
                Name = raw,
                NameLocation = GetLocation(start, end),
            };
            return;
        }

        currentProperty = new PropertyBuilder
        {
            IsDirective = true,
            StartOffset = start,
            Name = name,
        };
        if (raw == ".")
        {
            currentProperty.Modifiers.Add(CreateSimpleExpression("prop", false, StubLocation));
        }

        if (name == "pre")
        {
            inVPre = true;
            tokenizer.InVPre = true;
            currentVPreBoundary = currentOpenTag;

            // Convert directives collected before v-pre into plain attributes.
            var properties = currentOpenTag!.Properties;
            for (var i = 0; i < properties.Count; i++)
            {
                if (properties[i] is DirectiveNode directive)
                {
                    properties[i] = DirectiveToAttribute(directive);
                }
            }
        }
    }

    /// <inheritdoc />
    public void OnDirectiveArgument(int start, int end)
    {
        if (start == end)
        {
            return;
        }

        var argument = GetSlice(start, end);
        if (inVPre)
        {
            currentProperty!.Name += argument;
            currentProperty.NameLocation = ExtendLocationEnd(currentProperty.NameLocation!, end);
            return;
        }

        var isStatic = argument[0] != '[';
        currentProperty!.Argument = CreateSimpleExpression(
            isStatic ? argument : argument.Substring(1, argument.Length - 2),
            isStatic,
            GetLocation(start, end),
            isStatic ? ConstantType.CanStringify : ConstantType.NotConstant);
    }

    /// <inheritdoc />
    public void OnDirectiveModifier(int start, int end)
    {
        var modifier = GetSlice(start, end);
        if (inVPre)
        {
            currentProperty!.Name += "." + modifier;
            currentProperty.NameLocation = ExtendLocationEnd(currentProperty.NameLocation!, end);
        }
        else if (currentProperty!.Name == "slot")
        {
            // Slot has no modifiers: fold the segment back into the argument content.
            if (currentProperty.Argument is SimpleExpressionNode argument)
            {
                currentProperty.Argument = argument with
                {
                    Content = argument.Content + "." + modifier,
                    Location = ExtendLocationEnd(argument.Location, end),
                };
            }
        }
        else
        {
            currentProperty.Modifiers.Add(CreateSimpleExpression(modifier, true, GetLocation(start, end)));
        }
    }

    /// <inheritdoc />
    public void OnAttributeData(int start, int end)
    {
        currentAttributeValue.Append(GetSlice(start, end));
        if (currentAttributeStartIndex < 0)
        {
            currentAttributeStartIndex = start;
        }

        currentAttributeEndIndex = end;
    }

    /// <inheritdoc />
    public void OnAttributeNameEnd(int end)
    {
        var start = currentProperty!.StartOffset;
        var name = GetSlice(start, end);
        if (currentProperty.IsDirective)
        {
            currentProperty.RawName = name;
        }

        if (currentOpenTag!.Properties.Any(property =>
                (property is DirectiveNode directive ? directive.RawName : ((AttributeNode)property).Name) == name))
        {
            EmitError(CompilerErrorCode.DuplicateAttribute, start);
        }
    }

    /// <inheritdoc />
    public void OnAttributeEnd(QuoteType quote, int end)
    {
        if (currentOpenTag is not null && currentProperty is not null)
        {
            currentProperty.EndOffset = end;

            if (quote != QuoteType.NoValue)
            {
                var value = HtmlEntityDecoder.Decode(currentAttributeValue.ToString(), true);
                if (!currentProperty.IsDirective)
                {
                    if (currentProperty.Name == "class")
                    {
                        value = Condense(value).Trim();
                    }

                    if (quote == QuoteType.Unquoted && value.Length == 0)
                    {
                        EmitError(CompilerErrorCode.MissingAttributeValue, end);
                    }

                    var valueLocation = quote == QuoteType.Unquoted
                        ? GetLocation(currentAttributeStartIndex, currentAttributeEndIndex)
                        : GetLocation(currentAttributeStartIndex - 1, currentAttributeEndIndex + 1);
                    currentProperty.Value = new TextNode { Content = value, Location = valueLocation };
                }
                else
                {
                    currentProperty.Expression = CreateSimpleExpression(
                        value,
                        false,
                        GetLocation(currentAttributeStartIndex, currentAttributeEndIndex));
                }
            }

            if (!(currentProperty.IsDirective && currentProperty.Name == "pre"))
            {
                currentOpenTag.Properties.Add(FinalizeProperty(currentProperty));
            }
        }

        currentAttributeValue.Clear();
        currentAttributeStartIndex = currentAttributeEndIndex = -1;
    }

    /// <inheritdoc />
    public void OnComment(int start, int end)
    {
        if (options.KeepComments)
        {
            AddNode(new CommentNode { Content = GetSlice(start, end), Location = GetLocation(start - 4, end + 3) });
        }
    }

    /// <inheritdoc />
    public void OnCdata(int start, int end)
    {
        var parentNamespace = stack.Count > 0 ? stack[0].Namespace : options.RootNamespace;
        if (parentNamespace != ElementNamespace.Html)
        {
            AddText(GetSlice(start, end), start, end);
        }
        else
        {
            EmitError(CompilerErrorCode.CdataInHtmlContent, start - 9);
        }
    }

    /// <inheritdoc />
    public void OnProcessingInstruction(int start, int end)
    {
        var parentNamespace = stack.Count > 0 ? stack[0].Namespace : options.RootNamespace;
        if (parentNamespace == ElementNamespace.Html)
        {
            EmitError(CompilerErrorCode.UnexpectedQuestionMarkInsteadOfTagName, start - 1);
        }
    }

    /// <inheritdoc />
    public void OnEnd()
    {
        var end = input.Length;
        if (tokenizer.State != TokenizerState.Text)
        {
            switch (tokenizer.State)
            {
                case TokenizerState.BeforeTagName:
                case TokenizerState.BeforeClosingTagName:
                    EmitError(CompilerErrorCode.EofBeforeTagName, end);
                    break;
                case TokenizerState.Interpolation:
                case TokenizerState.InterpolationClose:
                    EmitError(CompilerErrorCode.XMissingInterpolationEnd, tokenizer.SectionStart);
                    break;
                case TokenizerState.InCommentLike:
                    EmitError(
                        tokenizer.CurrentSequence == TokenizerSequences.CdataEnd
                            ? CompilerErrorCode.EofInCdata
                            : CompilerErrorCode.EofInComment,
                        end);
                    break;
                case TokenizerState.InTagName:
                case TokenizerState.InSelfClosingTag:
                case TokenizerState.InClosingTagName:
                case TokenizerState.BeforeAttributeName:
                case TokenizerState.InAttributeName:
                case TokenizerState.InDirectiveName:
                case TokenizerState.InDirectiveArgument:
                case TokenizerState.InDirectiveDynamicArgument:
                case TokenizerState.InDirectiveModifier:
                case TokenizerState.AfterAttributeName:
                case TokenizerState.BeforeAttributeValue:
                case TokenizerState.InAttributeValueDoubleQuote:
                case TokenizerState.InAttributeValueSingleQuote:
                case TokenizerState.InAttributeValueNoQuote:
                    EmitError(CompilerErrorCode.EofInTag, end);
                    break;
                default:
                    break;
            }
        }

        for (var index = 0; index < stack.Count; index++)
        {
            var element = stack[index];
            var node = BuildElementNode(element, end - 1, false);
            var parentChildren = index + 1 < stack.Count ? stack[index + 1].Children : rootChildren;
            parentChildren.Add(node);
            EmitError(CompilerErrorCode.XMissingEndTag, element.TagStartOffset);
        }
    }

    /// <inheritdoc />
    public void OnError(CompilerErrorCode code, int index) => EmitError(code, index);

    // ---- Node assembly ----

    private void EndOpenTag(int end)
    {
        var element = currentOpenTag!;
        if (element.Namespace == ElementNamespace.Html && options.IsPreTag(element.Tag))
        {
            inPre++;
        }

        if (options.IsVoidTag(element.Tag))
        {
            AddToCurrentChildren(BuildElementNode(element, end, false));
        }
        else
        {
            stack.Insert(0, element);
            if (element.Namespace is ElementNamespace.Svg or ElementNamespace.MathML)
            {
                tokenizer.InXml = true;
            }
        }

        currentOpenTag = null;
    }

    private ElementNode BuildElementNode(ElementBuilder element, int end, bool isImplied)
    {
        var endOffset = isImplied ? BackTrack(end, '<') : LookAhead(end, '>') + 1;

        if (!inVPre)
        {
            if (element.Tag == "slot")
            {
                element.ElementType = ElementType.Slot;
            }
            else if (IsFragmentTemplate(element))
            {
                element.ElementType = ElementType.Template;
            }
            else if (IsComponent(element))
            {
                element.ElementType = ElementType.Component;
            }
        }

        IReadOnlyList<TemplateChildNode> children = tokenizer.InRcdata
            ? element.Children
            : CondenseWhitespace(element.Children);

        if (element.Namespace == ElementNamespace.Html && options.IsIgnoreNewlineTag(element.Tag) &&
            children.Count > 0 && children[0] is TextNode first)
        {
            var stripped = StripLeadingNewline(first.Content);
            if (!ReferenceEquals(stripped, first.Content))
            {
                var adjusted = children.ToList();
                adjusted[0] = first with { Content = stripped };
                children = adjusted;
            }
        }

        if (element.Namespace == ElementNamespace.Html && options.IsPreTag(element.Tag))
        {
            inPre--;
        }

        if (ReferenceEquals(currentVPreBoundary, element))
        {
            inVPre = false;
            tokenizer.InVPre = false;
            currentVPreBoundary = null;
        }

        if (tokenizer.InXml &&
            (stack.Count > 0 ? stack[0].Namespace : options.RootNamespace) == ElementNamespace.Html)
        {
            tokenizer.InXml = false;
        }

        return new ElementNode
        {
            Tag = element.Tag,
            Namespace = element.Namespace,
            ElementType = element.ElementType,
            IsSelfClosing = element.IsSelfClosing,
            Properties = new SyntaxList<PropertyNode>(element.Properties.ToArray()),
            Children = new SyntaxList<TemplateChildNode>(children.ToArray()),
            Location = GetLocation(element.TagStartOffset, endOffset),
        };
    }

    private PropertyNode FinalizeProperty(PropertyBuilder property)
    {
        var location = GetLocation(property.StartOffset, property.EndOffset);
        if (property.IsDirective)
        {
            return new DirectiveNode
            {
                Name = property.Name,
                RawName = property.RawName,
                Argument = property.Argument,
                Modifiers = new SyntaxList<SimpleExpressionNode>(property.Modifiers.ToArray()),
                Expression = property.Expression,
                Location = location,
            };
        }

        return new AttributeNode
        {
            Name = property.Name,
            NameLocation = property.NameLocation!,
            Value = property.Value,
            Location = location,
        };
    }

    private AttributeNode DirectiveToAttribute(DirectiveNode directive)
    {
        var rawName = directive.RawName ?? string.Empty;
        var nameLocation = GetLocation(
            directive.Location.Start.Offset,
            directive.Location.Start.Offset + rawName.Length);

        TextNode? value = null;
        if (directive.Expression is SimpleExpressionNode expression)
        {
            var location = expression.Location;
            if (location.End.Offset < directive.Location.End.Offset)
            {
                // Account for the surrounding quotes; recompute the slice so loc.Source stays exact.
                location = GetLocation(location.Start.Offset - 1, location.End.Offset + 1);
            }

            value = new TextNode { Content = expression.Content, Location = location };
        }

        return new AttributeNode
        {
            Name = rawName,
            NameLocation = nameLocation,
            Value = value,
            Location = directive.Location,
        };
    }

    // ---- Element classification (port of parser.ts isComponent / isFragmentTemplate) ----

    private bool IsFragmentTemplate(ElementBuilder element)
    {
        if (element.Tag != "template")
        {
            return false;
        }

        foreach (var property in element.Properties)
        {
            if (property is DirectiveNode directive && SpecialTemplateDirectives.Contains(directive.Name))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsComponent(ElementBuilder element)
    {
        var tag = element.Tag;
        if (options.IsCustomElement(tag))
        {
            return false;
        }

        if (tag == "component" ||
            IsUpperCase(tag[0]) ||
            IsCoreComponent(tag) ||
            (options.IsBuiltInComponent?.Invoke(tag) ?? false) ||
            (options.IsNativeTag is not null && !options.IsNativeTag(tag)))
        {
            return true;
        }

        foreach (var property in element.Properties)
        {
            if (property is AttributeNode { Name: "is", Value: { } value } && value.Content.StartsWith("vue:", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCoreComponent(string tag) => tag switch
    {
        "Teleport" or "teleport" => true,
        "Suspense" or "suspense" => true,
        "KeepAlive" or "keep-alive" => true,
        "BaseTransition" or "base-transition" => true,
        _ => false,
    };

    private static bool IsUpperCase(char character) => character > 64 && character < 91;

    // ---- Whitespace management (port of parser.ts condenseWhitespace) ----

    private List<TemplateChildNode> CondenseWhitespace(List<TemplateChildNode> nodes)
    {
        var shouldCondense = options.Whitespace != WhitespaceStrategy.Preserve;
        var working = new TemplateChildNode?[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            working[i] = nodes[i];
        }

        for (var i = 0; i < working.Length; i++)
        {
            if (working[i] is not TextNode node)
            {
                continue;
            }

            if (inPre == 0)
            {
                if (IsAllWhitespace(node.Content))
                {
                    var previous = i > 0 ? working[i - 1]?.NodeType : null;
                    var next = i < working.Length - 1 ? working[i + 1]?.NodeType : null;
                    if (previous is null ||
                        next is null ||
                        (shouldCondense &&
                         ((previous == NodeType.Comment && (next == NodeType.Comment || next == NodeType.Element)) ||
                          (previous == NodeType.Element &&
                           (next == NodeType.Comment ||
                            (next == NodeType.Element && HasNewlineChar(node.Content)))))))
                    {
                        working[i] = null;
                    }
                    else
                    {
                        working[i] = node with { Content = " " };
                    }
                }
                else if (shouldCondense)
                {
                    working[i] = node with { Content = Condense(node.Content) };
                }
            }
            else
            {
                working[i] = node with { Content = node.Content.Replace("\r\n", "\n") };
            }
        }

        var result = new List<TemplateChildNode>(working.Length);
        foreach (var candidate in working)
        {
            if (candidate is not null)
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private static bool IsAllWhitespace(string value)
    {
        foreach (var character in value)
        {
            if (!IsWhitespace(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasNewlineChar(string value)
    {
        foreach (var character in value)
        {
            if (character is '\n' or '\r')
            {
                return true;
            }
        }

        return false;
    }

    private static string Condense(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousIsWhitespace = false;
        foreach (var character in value)
        {
            if (IsWhitespace(character))
            {
                if (!previousIsWhitespace)
                {
                    builder.Append(' ');
                    previousIsWhitespace = true;
                }
            }
            else
            {
                builder.Append(character);
                previousIsWhitespace = false;
            }
        }

        return builder.ToString();
    }

    private static string StripLeadingNewline(string content)
    {
        if (content.Length >= 1 && content[0] == '\n')
        {
            return content.Substring(1);
        }

        if (content.Length >= 2 && content[0] == '\r' && content[1] == '\n')
        {
            return content.Substring(2);
        }

        return content;
    }

    // ---- Text / node helpers ----

    private bool ShouldDecodeText()
    {
        if (stack.Count == 0)
        {
            return true;
        }

        var tag = stack[0].Tag;
        return tag != "script" && tag != "style";
    }

    private void AddText(string content, int start, int end)
    {
        var children = CurrentChildren;
        if (children.Count > 0 && children[children.Count - 1] is TextNode last)
        {
            var mergedStart = last.Location.Start;
            children[children.Count - 1] = last with
            {
                Content = last.Content + content,
                Location = new SourceLocation(mergedStart, GetPosition(end), GetSlice(mergedStart.Offset, end)),
            };
        }
        else
        {
            children.Add(new TextNode { Content = content, Location = GetLocation(start, end) });
        }
    }

    private void AddNode(TemplateChildNode node) => CurrentChildren.Add(node);

    private void AddToCurrentChildren(TemplateChildNode node) => CurrentChildren.Add(node);

    private List<TemplateChildNode> CurrentChildren => stack.Count > 0 ? stack[0].Children : rootChildren;

    private ElementNode? ParentSnapshot()
    {
        if (stack.Count == 0)
        {
            return null;
        }

        var parent = stack[0];
        return new ElementNode
        {
            Tag = parent.Tag,
            Namespace = parent.Namespace,
            ElementType = parent.ElementType,
            IsSelfClosing = parent.IsSelfClosing,
            Properties = new SyntaxList<PropertyNode>(parent.Properties.ToArray()),
            Children = SyntaxList<TemplateChildNode>.Empty,
            Location = StubLocation,
        };
    }

    private static SimpleExpressionNode CreateSimpleExpression(
        string content,
        bool isStatic,
        SourceLocation location,
        ConstantType constantType = ConstantType.NotConstant)
        => new()
        {
            Content = content,
            IsStatic = isStatic,
            ConstantType = isStatic ? ConstantType.CanStringify : constantType,
            Location = location,
        };

    private void EmitError(CompilerErrorCode code, int index)
        => options.OnError?.Invoke(new CompilerError(code, CompilerErrorMessages.GetMessage(code), GetLocation(index, index)));

    // ---- Source slicing / positions ----

    private string GetSlice(int start, int end)
    {
        if (start < 0)
        {
            start = 0;
        }

        if (end > input.Length)
        {
            end = input.Length;
        }

        if (end < start)
        {
            end = start;
        }

        return input.Substring(start, end - start);
    }

    private SourceLocation GetLocation(int start, int end)
        => new(GetPosition(start), GetPosition(end), GetSlice(start, end));

    private SourceLocation ExtendLocationEnd(SourceLocation location, int end)
        => new(location.Start, GetPosition(end), GetSlice(location.Start.Offset, end));

    private Position GetPosition(int offset) => tokenizer.GetPosition(offset);

    private int LookAhead(int start, char target)
    {
        var i = start < 0 ? 0 : start;
        while (i < input.Length - 1 && input[i] != target)
        {
            i++;
        }

        return i;
    }

    private int BackTrack(int start, char target)
    {
        var i = start >= input.Length ? input.Length - 1 : start;
        while (i >= 0 && input[i] != target)
        {
            i--;
        }

        return i;
    }

    private static bool IsWhitespace(char character) => character is ' ' or '\n' or '\t' or '\f' or '\r';
}
