using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Microsoft.CodeAnalysis.CSharp;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The code-generation worker behind <see cref="RenderFunctionEmitter"/>: a line-oriented writer over the
/// transformed code-generation tree, the C# port of the <c>CodegenContext</c> plus the <c>genNode</c>
/// family in Vue 3.5's <c>@vue/compiler-core</c> <c>codegen.ts</c>. Each <c>EmitXxx</c> method mirrors the
/// upstream <c>genXxx</c> function of the same node kind; the divergences forced by C# (no comma operator,
/// no object/array literals with untyped targets, no <c>undefined</c>) are centralized in
/// <see cref="EmitVNodeCall"/>, <see cref="EmitObjectExpression"/>, <see cref="EmitNodeListAsArray{T}"/>,
/// and <see cref="MapRawLiteral"/>, and documented in the library's <c>docs/DESIGN.md</c>.
/// </summary>
internal sealed class RenderCodeWriter
{
    // ---- Viu-defined contract helper names (no upstream counterpart; see docs/DESIGN.md). ----

    // JavaScript object literals have no C# equivalent, so props/slots objects emit through this helper.
    private const string CreatePropsHelper = "_createProps";

    // C# lambdas and method groups have no natural type in an object-typed position, so event-handler
    // property values emit through this delegate-typed helper.
    private const string WithHandlerHelper = "_withHandler";

    // Upstream's cached-vnode comma sequence (setBlockTracking(-1), (_cache[n] = ...).cacheIndex = n,
    // setBlockTracking(1), _cache[n]) collapses into this helper; C# left-to-right argument evaluation
    // keeps the pause-create-resume ordering.
    private const string SetCacheHelper = "_setCache";

    // Upstream's cached-array spread [...(_cache[n] || ...)] becomes a cloning helper call.
    private const string SpreadCacheHelper = "_spreadCache";

    private readonly TransformResult result;
    private readonly StringBuilder builder = new();
    private readonly string indentText;
    private int indentLevel;

    // Render source map ([V01.01.05.08]): each dynamic expression's original-template-text offset within
    // the emitted output, paired with its template location. Converted to line/column against the finished
    // code once (BuildSourceMappings), so the generator can emit #line span directives that land a C#
    // compile error inside an emitted expression on the offending .viu template coordinate.
    private readonly List<(int Offset, SourceLocation Location)> mappingSites = new();
    private IReadOnlyList<RenderSourceMapping> sourceMappings = Array.Empty<RenderSourceMapping>();

    /// <summary>The render source map produced by the most recent <see cref="EmitRenderBody"/> call.</summary>
    public IReadOnlyList<RenderSourceMapping> SourceMappings => sourceMappings;

    /// <summary>Creates a writer for <paramref name="result"/> starting at <paramref name="indentLevel"/>.</summary>
    /// <param name="result">The transformed template whose code-generation tree is serialized.</param>
    /// <param name="indentLevel">The starting indentation level.</param>
    /// <param name="indentText">The text of one indentation level.</param>
    public RenderCodeWriter(TransformResult result, int indentLevel, string indentText)
    {
        this.result = result;
        this.indentLevel = indentLevel;
        this.indentText = indentText;
    }

    /// <summary>
    /// Emits the full render-method body: the asset-resolution statements (upstream <c>genAssets</c>)
    /// followed by the <c>return</c> of the root block expression (the tail of upstream <c>generate</c>).
    /// </summary>
    /// <returns>The emitted statements, LF-terminated.</returns>
    public string EmitRenderBody()
    {
        AppendIndent();

        // Port of genAssets: components first, then directives, in registration order. The asset id is
        // the same toValidAssetId spelling transformElement stamped into the vnode tags/directive arrays.
        var hasAssets = false;
        foreach (var component in result.Components)
        {
            if (hasAssets)
            {
                Newline();
            }

            Push("var ");
            Push(TransformElement.ToValidAssetId(component, "component"));
            Push(" = ");
            Push(Helper(HelperNames.ResolveComponent));
            Push("(");
            Push(StringLiteral(component));
            Push(");");
            hasAssets = true;
        }

        foreach (var directive in result.Directives)
        {
            if (hasAssets)
            {
                Newline();
            }

            Push("var ");
            Push(TransformElement.ToValidAssetId(directive, "directive"));
            Push(" = ");
            Push(Helper(HelperNames.ResolveDirective));
            Push("(");
            Push(StringLiteral(directive));
            Push(");");
            hasAssets = true;
        }

        if (hasAssets)
        {
            // A separating blank line between the asset preamble and the return, as upstream emits.
            builder.Append('\n').Append('\n');
            AppendIndent();
        }

        Push("return ");
        if (result.CodegenNode is { } root)
        {
            EmitNode(root);
        }
        else
        {
            // An empty template renders nothing (upstream: push("null")).
            Push("null");
        }

        Push(";");
        builder.Append('\n');
        var code = builder.ToString();
        sourceMappings = BuildSourceMappings(code);
        return code;
    }

    // ---- genNode: the central dispatch (codegen.ts genNode). ----

    private void EmitNode(object? node)
    {
        switch (node)
        {
            case null:
                Push("null");
                break;
            case string raw:
                Push(MapRawLiteral(raw));
                break;
            case RuntimeHelper helper:
                Push(Helper(helper));
                break;
            case SyntaxList<TemplateChildNode> children:
                EmitNodeListAsArray(children);
                break;
            case ElementNode element:
                // ELEMENT defers to its codegen node; this port resolves it through the transform's
                // side table rather than an in-place mutated field.
                EmitNode(result.GetCodegenNode(element));
                break;
            case IfNode ifNode:
                EmitNode(ifNode.CodegenNode);
                break;
            case ForNode forNode:
                EmitNode(forNode.CodegenNode);
                break;
            case TextCallNode textCall:
                EmitNode(textCall.CodegenNode);
                break;
            case TextNode text:
                // genText: the static text as a string literal (upstream JSON.stringify).
                Push(StringLiteral(text.Content));
                break;
            case SimpleExpressionNode simple:
                EmitExpression(simple);
                break;
            case InterpolationNode interpolation:
                EmitInterpolation(interpolation);
                break;
            case CompoundExpressionNode compound:
                EmitCompoundExpression(compound);
                break;
            case CommentNode comment:
                EmitComment(comment);
                break;
            case VNodeCall vnodeCall:
                EmitVNodeCall(vnodeCall);
                break;
            case CallExpression call:
                EmitCallExpression(call);
                break;
            case ObjectExpression objectExpression:
                EmitObjectExpression(objectExpression);
                break;
            case ArrayExpression array:
                EmitNodeListAsArray(array.Elements);
                break;
            case FunctionExpression function:
                EmitFunctionExpression(function);
                break;
            case ConditionalExpression conditional:
                EmitConditionalExpression(conditional);
                break;
            case CacheExpression cache:
                EmitCacheExpression(cache, wrapHandler: false);
                break;
            case BlockStatement block:
                EmitBlockStatement(block);
                break;
            default:
                // The transform pipeline only produces the kinds above (upstream's switch is likewise
                // exhaustive); anything else is a pipeline bug worth failing loudly in tests.
                throw new InvalidOperationException(
                    $"Unsupported code-generation node '{node.GetType().Name}'.");
        }
    }

    // ---- genVNodeCall. ----

    private void EmitVNodeCall(VNodeCall node)
    {
        if (node.Directives is not null)
        {
            Push(Helper(HelperNames.WithDirectives));
            Push("(");
        }

        var callee = node.IsBlock
            ? Helper(TransformContext.GetVNodeBlockHelper(ssr: false, node.IsComponent))
            : Helper(TransformContext.GetVNodeHelper(ssr: false, node.IsComponent));
        Push(callee);
        Push("(");

        if (node.IsBlock)
        {
            // Upstream emits the comma sequence `(openBlock(), createElementBlock(...))`; C# has no
            // comma operator, so the open-block call rides as the first argument — C# guarantees
            // left-to-right argument evaluation, so the block opens before any child argument is
            // evaluated and the block factory closes it (see docs/DESIGN.md).
            Push(Helper(HelperNames.OpenBlock));
            Push(node.DisableTracking ? "(true), " : "(), ");
        }

        var arguments = TrimTrailingNulls(new object?[]
        {
            node.Tag,
            node.Props,
            node.Children,
            PatchFlagText(node.PatchFlag),
            node.DynamicProps,
        });
        EmitNodeList(arguments);
        Push(")");

        if (node.Directives is not null)
        {
            Push(", ");
            EmitNode(node.Directives);
            Push(")");
        }
    }

    // ---- genCallExpression. ----

    private void EmitCallExpression(CallExpression node)
    {
        var callee = node.Callee is RuntimeHelper helper ? Helper(helper) : MapRawLiteral((string)node.Callee);
        Push(callee);
        Push("(");
        EmitNodeList(node.Arguments);
        Push(")");
    }

    // ---- genObjectExpression: JavaScript object literals become _createProps entries. ----

    private void EmitObjectExpression(ObjectExpression node)
    {
        Push(CreatePropsHelper);
        Push("(");
        var properties = node.Properties;
        if (properties.Count == 0)
        {
            Push(")");
            return;
        }

        // Upstream splits multi-property objects across lines; a single property stays inline.
        var multiline = properties.Count > 1;
        if (multiline)
        {
            IndentIn();
        }

        for (var index = 0; index < properties.Count; index++)
        {
            EmitProperty(properties[index]);
            if (index < properties.Count - 1)
            {
                Push(",");
                if (multiline)
                {
                    Newline();
                }
                else
                {
                    Push(" ");
                }
            }
        }

        if (multiline)
        {
            IndentOut();
        }

        Push(")");
    }

    private void EmitProperty(Property node)
    {
        Push("(");
        EmitPropertyKey(node.Key);
        Push(", ");
        if (IsHandlerKey(node.Key))
        {
            EmitHandlerValue(node.Value);
        }
        else
        {
            EmitNode(node.Value);
        }

        Push(")");
    }

    // Port of genExpressionAsPropertyKey. Upstream emits bare identifiers for static keys and
    // `[expression]` computed keys; the C# props entry is a (string, object?) tuple, so a static key is
    // a string literal and a dynamic key is emitted as its (string-typed by contract) expression.
    private void EmitPropertyKey(ExpressionNode key)
    {
        switch (key)
        {
            case CompoundExpressionNode compound:
                EmitCompoundExpression(compound);
                break;
            case SimpleExpressionNode { IsStatic: true } staticKey:
                Push(StringLiteral(staticKey.Content));
                break;
            case SimpleExpressionNode dynamicKey:
                // A dynamic (computed) key is a template expression too; route it through EmitExpression so
                // it participates in the render source map ([V01.01.05.08]) like any other dynamic access.
                EmitExpression(dynamicKey);
                break;
            default:
                EmitNode(key);
                break;
        }
    }

    // The upstream isOn discipline (@vue/shared onRE /^on[^a-z]/) plus the transforms' explicit
    // IsHandlerKey marks: these properties carry event handlers, whose values need the delegate-typed
    // _withHandler wrapper in C# (a lambda or method group has no natural type in an object position).
    private static bool IsHandlerKey(ExpressionNode key) => key switch
    {
        SimpleExpressionNode { IsHandlerKey: true } => true,
        CompoundExpressionNode { IsHandlerKey: true } => true,
        SimpleExpressionNode { IsStatic: true } simple => IsOnKey(simple.Content),
        _ => false,
    };

    private static bool IsOnKey(string name)
        => name.Length > 2 && name[0] == 'o' && name[1] == 'n' && !(name[2] >= 'a' && name[2] <= 'z');

    private void EmitHandlerValue(TemplateSyntaxNode value)
    {
        switch (value)
        {
            case CacheExpression cache:
                EmitCacheExpression(cache, wrapHandler: true);
                break;
            case CallExpression { Callee: RuntimeHelper helper } call
                when helper == HelperNames.WithModifiers || helper == HelperNames.WithKeys:
                // The guard wrapper's own (contract-typed) parameter gives the inner lambda its
                // delegate target type; no extra wrapper is needed.
                EmitCallExpression(call);
                break;
            case ExpressionNode expression:
                Push(WithHandlerHelper);
                Push("(");
                EmitNode(expression);
                Push(")");
                break;
            default:
                EmitNode(value);
                break;
        }
    }

    // ---- genFunctionExpression. ----

    private void EmitFunctionExpression(FunctionExpression node)
    {
        if (node.IsSlot)
        {
            Push(Helper(HelperNames.WithCtx));
            Push("(");
        }

        Push("(");
        EmitNodeList(node.Parameters);
        Push(") => ");

        var braced = node.Newline || node.Body is not null;
        if (braced)
        {
            Push("{");
            IndentIn();
        }

        if (node.Returns is { } returns)
        {
            if (node.Newline)
            {
                Push("return ");
            }

            if (returns is SyntaxList<TemplateChildNode> list)
            {
                EmitNodeListAsArray(list);
            }
            else
            {
                EmitNode(returns);
            }

            if (node.Newline)
            {
                // C# return statements are semicolon-terminated (upstream JavaScript omits it).
                Push(";");
            }
        }
        else if (node.Body is { } body)
        {
            EmitBlockStatement(body);
        }

        if (braced)
        {
            IndentOut();
            Push("}");
        }

        if (node.IsSlot)
        {
            Push(")");
        }
    }

    // ---- genConditionalExpression. ----

    private void EmitConditionalExpression(ConditionalExpression node)
    {
        if (node.Test is SimpleExpressionNode simpleTest)
        {
            var needsParens = !IsSimpleIdentifier(simpleTest.Content);
            if (needsParens)
            {
                Push("(");
            }

            EmitExpression(simpleTest);
            if (needsParens)
            {
                Push(")");
            }
        }
        else
        {
            Push("(");
            EmitNode(node.Test);
            Push(")");
        }

        var needNewline = node.Newline;
        if (needNewline)
        {
            IndentIn();
        }

        indentLevel++;
        if (!needNewline)
        {
            Push(" ");
        }

        Push("? ");
        EmitNode(node.Consequent);
        indentLevel--;
        if (needNewline)
        {
            Newline();
        }
        else
        {
            Push(" ");
        }

        Push(": ");
        var isNested = node.Alternate is ConditionalExpression;
        if (!isNested)
        {
            indentLevel++;
        }

        EmitNode(node.Alternate);
        if (!isNested)
        {
            indentLevel--;
        }

        if (needNewline)
        {
            // deindent(withoutNewline: true)
            indentLevel--;
        }
    }

    // ---- genCacheExpression: `_cache[n] || (_cache[n] = ...)` becomes `_cache[n] ??= ...`. ----

    private void EmitCacheExpression(CacheExpression node, bool wrapHandler)
    {
        var index = node.Index.ToString(CultureInfo.InvariantCulture);
        if (node.NeedArraySpread)
        {
            Push(SpreadCacheHelper);
            Push("(");
        }

        Push("(_cache[");
        Push(index);
        Push("] ??= ");

        if (node.NeedPauseTracking)
        {
            // Upstream pauses block tracking, creates the value, stamps value.cacheIndex, resumes, and
            // yields the slot — a comma sequence. The C# form rides on argument evaluation order: the
            // setBlockTracking(-1) argument pauses tracking before the value argument is evaluated, and
            // _setCache stamps the index, resumes tracking, and returns the value.
            Push(SetCacheHelper);
            Push("(");
            Push(index);
            Push(", ");
            Push(Helper(HelperNames.SetBlockTracking));
            Push(node.InVOnce ? "(-1, true), " : "(-1), ");
            EmitCachedValue(node, wrapHandler);
            Push(")");
        }
        else
        {
            EmitCachedValue(node, wrapHandler);
        }

        Push(")");
        if (node.NeedArraySpread)
        {
            Push(")");
        }
    }

    private void EmitCachedValue(CacheExpression node, bool wrapHandler)
    {
        if (wrapHandler)
        {
            EmitHandlerValue(node.Value);
        }
        else
        {
            EmitNode(node.Value);
        }
    }

    // ---- genNodeList / genNodeListAsArray. ----

    private void EmitNodeList<T>(IReadOnlyList<T?> nodes, bool multiline = false)
        where T : class
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            EmitNode(nodes[index]);
            if (index < nodes.Count - 1)
            {
                if (multiline)
                {
                    Push(",");
                    Newline();
                }
                else
                {
                    Push(", ");
                }
            }
        }
    }

    // JavaScript array literals `[...]` have no untyped C# counterpart, so child/argument arrays emit as
    // `new object?[] { ... }` — the runtime helper surface accepts object-typed children and normalizes,
    // exactly as Vue's runtime normalizes unknown children (see docs/DESIGN.md).
    private void EmitNodeListAsArray<T>(IReadOnlyList<T?> nodes)
        where T : class
    {
        var multiline = nodes.Count > 3;
        Push("new object?[] {");
        if (multiline)
        {
            IndentIn();
        }
        else
        {
            Push(" ");
        }

        EmitNodeList(nodes, multiline);
        if (multiline)
        {
            IndentOut();
        }
        else
        {
            Push(" ");
        }

        Push("}");
    }

    // ---- genExpression / genInterpolation / genCompoundExpression / genComment / block statements. ----

    private void EmitExpression(SimpleExpressionNode node)
    {
        if (node.IsStatic)
        {
            Push(StringLiteral(node.Content));
            return;
        }

        var text = MapRawLiteral(node.Content);
        RecordSourceMapping(node.Location, text);
        Push(text);
    }

    // ---- render source map ([V01.01.05.08]) ----

    // Records a mapping site for a dynamic expression: the absolute offset of its original template text
    // within the emitted output (past any inserted _ctx. prefix or unref(...) wrapper) paired with its
    // template location. builder.Length is captured before the text is pushed, so it is the offset the
    // emitted text will occupy. Synthetic nodes (empty-source stub location) and any emission whose
    // original source is not a recognizable substring (a hoist placeholder such as `_hoisted_1`) are
    // skipped — they have no faithful template span to point a #line directive at.
    private void RecordSourceMapping(SourceLocation location, string emittedText)
    {
        var source = location.Source;
        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        var inner = emittedText.IndexOf(source, StringComparison.Ordinal);
        if (inner < 0)
        {
            return;
        }

        mappingSites.Add((builder.Length + inner, location));
    }

    private IReadOnlyList<RenderSourceMapping> BuildSourceMappings(string code)
    {
        if (mappingSites.Count == 0)
        {
            return Array.Empty<RenderSourceMapping>();
        }

        var mappings = new List<RenderSourceMapping>(mappingSites.Count);
        foreach (var (offset, location) in mappingSites)
        {
            var (line, column) = LineColumnAt(code, offset);
            mappings.Add(new RenderSourceMapping
            {
                GeneratedLine = line,
                GeneratedColumn = column,
                TemplateLocation = location,
            });
        }

        return mappings;
    }

    // The zero-based (line, column) of an absolute character offset within the emitted body, by counting
    // the LF newlines the emitter deterministically writes.
    private static (int Line, int Column) LineColumnAt(string text, int offset)
    {
        var line = 0;
        var lineStart = 0;
        var limit = offset < text.Length ? offset : text.Length;
        for (var index = 0; index < limit; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }

        return (line, offset - lineStart);
    }

    private void EmitInterpolation(InterpolationNode node)
    {
        Push(Helper(HelperNames.ToDisplayString));
        Push("(");
        EmitNode(node.Content);
        Push(")");
    }

    private void EmitCompoundExpression(CompoundExpressionNode node)
    {
        foreach (var part in node.Parts)
        {
            switch (part)
            {
                case string raw:
                    Push(MapRawLiteral(raw));
                    break;
                case RuntimeHelper helper:
                    Push(Helper(helper));
                    break;
                default:
                    EmitNode(part);
                    break;
            }
        }
    }

    private void EmitComment(CommentNode node)
    {
        Push(Helper(HelperNames.CreateComment));
        Push("(");
        Push(StringLiteral(node.Content));
        Push(")");
    }

    private void EmitBlockStatement(BlockStatement node)
    {
        for (var index = 0; index < node.Body.Count; index++)
        {
            EmitNode(node.Body[index]);
            // C# statements are semicolon-terminated (upstream JavaScript relies on ASI).
            Push(";");
            if (index < node.Body.Count - 1)
            {
                Newline();
            }
        }
    }

    // ---- patch-flag formatting: numeric parity plus upstream's PatchFlagNames comment. ----

    private static string? PatchFlagText(PatchFlags? patchFlag)
    {
        if (patchFlag is not { } flag || flag == 0)
        {
            return null;
        }

        var value = (int)flag;
        var text = value.ToString(CultureInfo.InvariantCulture);
        return text + " /* " + PatchFlagNames(value) + " */";
    }

    private static string PatchFlagNames(int value)
    {
        // The upstream PatchFlagNames map (@vue/shared patchFlags.ts, v3.5 spelling — CACHED, not the
        // pre-3.5 HOISTED). Negative flags are whole-value sentinels, never combined.
        if (value < 0)
        {
            return value == -1 ? "CACHED" : "BAIL";
        }

        var names = new List<string>();
        AppendFlagName(names, value, 1, "TEXT");
        AppendFlagName(names, value, 1 << 1, "CLASS");
        AppendFlagName(names, value, 1 << 2, "STYLE");
        AppendFlagName(names, value, 1 << 3, "PROPS");
        AppendFlagName(names, value, 1 << 4, "FULL_PROPS");
        AppendFlagName(names, value, 1 << 5, "NEED_HYDRATION");
        AppendFlagName(names, value, 1 << 6, "STABLE_FRAGMENT");
        AppendFlagName(names, value, 1 << 7, "KEYED_FRAGMENT");
        AppendFlagName(names, value, 1 << 8, "UNKEYED_FRAGMENT");
        AppendFlagName(names, value, 1 << 9, "NEED_PATCH");
        AppendFlagName(names, value, 1 << 10, "DYNAMIC_SLOTS");
        AppendFlagName(names, value, 1 << 11, "DEV_ROOT_FRAGMENT");
        return string.Join(", ", names);
    }

    private static void AppendFlagName(List<string> names, int value, int flag, string name)
    {
        if ((value & flag) != 0)
        {
            names.Add(name);
        }
    }

    // ---- literal bridging: the JavaScript spellings the transforms carry as raw strings. ----

    // The transform pipeline mirrors upstream IR literally, including a handful of JavaScript literal
    // spellings inside raw strings; serialization maps them to their C# equivalents (see docs/DESIGN.md).
    private static string MapRawLiteral(string raw)
    {
        switch (raw)
        {
            case "undefined":
            case "void 0":
                return "null";
            case "{}":
                // The empty props object (renderSlot's placeholder argument).
                return CreatePropsHelper + "()";
            case "$slots":
                // The slot outlet source: inline-mode upstream emits _ctx.$slots; `$` is not legal in a
                // C# identifier, so the contract spelling is _ctx.__slots (the __event precedent).
                return "_ctx.__slots";
            default:
                break;
        }

        var mapped = raw;
        if (mapped.StartsWith("const ", StringComparison.Ordinal))
        {
            // The v-memo loop body's `const` statements (JavaScript) become `var` (C#).
            mapped = "var " + mapped.Substring(6);
        }

        if (mapped.IndexOf("$event", StringComparison.Ordinal) >= 0)
        {
            // Vue's event variable, wherever a transform spelled it into a raw part (the v-model
            // assignment handler builds `$event => (... = $event)` itself): the Viu C# spelling is
            // __event, the same substitution v-on applies under prefixing ([V01.01.05.04]).
            mapped = mapped.Replace("$event", "__event");
        }

        if (mapped.IndexOf(" === ", StringComparison.Ordinal) >= 0)
        {
            mapped = mapped.Replace(" === ", " == ");
        }

        if (mapped.IndexOf(" !== ", StringComparison.Ordinal) >= 0)
        {
            mapped = mapped.Replace(" !== ", " != ");
        }

        return mapped;
    }

    // ---- plumbing (codegen.ts push/indent/deindent/newline). ----

    private static string Helper(RuntimeHelper helper) => "_" + helper.Name;

    private static string StringLiteral(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    private static bool IsSimpleIdentifier(string content)
    {
        if (content.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            var isLetter = (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z') || character == '_';
            var isDigit = character >= '0' && character <= '9';
            if (!(isLetter || (index > 0 && isDigit)))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<object?> TrimTrailingNulls(object?[] arguments)
    {
        var count = arguments.Length;
        while (count > 0 && arguments[count - 1] is null)
        {
            count--;
        }

        if (count == arguments.Length)
        {
            return arguments;
        }

        var trimmed = new object?[count];
        Array.Copy(arguments, trimmed, count);
        return trimmed;
    }

    private void Push(string text) => builder.Append(text);

    private void Newline()
    {
        builder.Append('\n');
        AppendIndent();
    }

    private void AppendIndent()
    {
        for (var level = 0; level < indentLevel; level++)
        {
            builder.Append(indentText);
        }
    }

    private void IndentIn()
    {
        indentLevel++;
        Newline();
    }

    private void IndentOut()
    {
        indentLevel--;
        Newline();
    }
}
