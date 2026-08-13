using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Lowers a server-transformed template to ordered writes over the public ServerRenderer compiler
/// contract. Native markup stays on the string path; components and unsupported property shapes use
/// subtree-local virtual-node fallbacks emitted by <see cref="FrameRenderCodeWriter"/>.
/// </summary>
internal sealed class ServerRenderCodeWriter
{
    private const string ComponentsNamespace = "global::Assimalign.Viu.Components";
    private const string CoreNamespace = "global::Assimalign.Viu";
    private const string ServerNamespace = "global::Assimalign.Viu.ServerRenderer";

    private readonly TransformResult result;
    private readonly StringBuilder builder = new();
    private readonly StringBuilder staticMarkup = new();
    private readonly string indentText;
    private readonly List<(int Offset, SourceLocation Location)> mappingSites = new();
    private IReadOnlyList<RenderSourceMapping> sourceMappings = Array.Empty<RenderSourceMapping>();
    private string staticStateName = "state";
    private int indentLevel;
    private int generatedNameIndex;

    internal ServerRenderCodeWriter(
        TransformResult result,
        int indentLevel,
        string indentText)
    {
        this.result = result;
        this.indentLevel = indentLevel;
        this.indentText = indentText;
    }

    /// <summary>Gets the source map produced by the latest server-body emission.</summary>
    internal IReadOnlyList<RenderSourceMapping> SourceMappings => sourceMappings;

    /// <summary>Emits one asynchronous compiled-server-render method body.</summary>
    internal string EmitRenderBody()
    {
        EmitChildrenAsRoot(result.Children, "state", selectModel: null);
        FlushStaticMarkup();
        string code = builder.ToString();
        sourceMappings = BuildSourceMappings(code);
        return code;
    }

    private void EmitChildrenAsRoot(
        IReadOnlyList<TemplateChildNode> children,
        string stateName,
        CodeExpression? selectModel)
    {
        if (children.Count > 1)
        {
            AppendMarker(stateName, "FragmentStart");
        }

        EmitChildren(children, stateName, selectModel);

        if (children.Count > 1)
        {
            AppendMarker(stateName, "FragmentEnd");
        }
    }

    private void EmitChildren(
        IReadOnlyList<TemplateChildNode> children,
        string stateName,
        CodeExpression? selectModel)
    {
        for (int index = 0; index < children.Count; index++)
        {
            EmitNode(children[index], stateName, selectModel);
        }
    }

    private void EmitNode(
        TemplateChildNode node,
        string stateName,
        CodeExpression? selectModel)
    {
        switch (node)
        {
            case TextNode text:
                AppendStaticMarkup(stateName, EscapeHtml(text.Content));
                break;
            case InterpolationNode interpolation:
                AppendStateHelper(
                    stateName,
                    "SsrInterpolate",
                    EmitExpression(interpolation.Content));
                break;
            case TextCallNode textCall:
                EmitTextCall(textCall, stateName);
                break;
            case CommentNode comment:
                AppendStateHelper(
                    stateName,
                    "SsrRenderComment",
                    CodeExpression.Literal(FrameRenderCodeWriter.StringLiteral(comment.Content)));
                break;
            case ElementNode element:
                EmitElement(element, stateName, selectModel);
                break;
            case IfNode conditional:
                EmitIf(conditional, stateName, selectModel);
                break;
            case ForNode loop:
                EmitFor(loop, stateName, selectModel);
                break;
            default:
                EmitFallback(node, stateName);
                break;
        }
    }

    private void EmitTextCall(TextCallNode textCall, string stateName)
    {
        switch (textCall.Content)
        {
            case TextNode text:
                AppendStaticMarkup(stateName, EscapeHtml(text.Content));
                break;
            case InterpolationNode interpolation:
                AppendStateHelper(
                    stateName,
                    "SsrInterpolate",
                    EmitExpression(interpolation.Content));
                break;
            case CompoundExpressionNode compound:
                AppendStateHelper(stateName, "SsrInterpolate", EmitExpression(compound));
                break;
            default:
                AppendStateHelper(stateName, "SsrInterpolate", EmitExpression(textCall.Content));
                break;
        }
    }

    private void EmitElement(
        ElementNode element,
        string stateName,
        CodeExpression? selectModel)
    {
        if (element.ElementType == ElementType.Template)
        {
            EmitChildrenAsRoot(ChildrenOf(element), stateName, selectModel);
            return;
        }

        if (element.ElementType != ElementType.Element)
        {
            if (IsTeleport(element.Tag))
            {
                EmitTeleport(element, stateName);
            }
            else if (IsSuspense(element.Tag))
            {
                EmitSuspense(element, stateName);
            }
            else if (IsTransition(element.Tag) || IsKeepAlive(element.Tag))
            {
                EmitChildrenAsRoot(DefaultContentOf(element), stateName, selectModel: null);
            }
            else
            {
                EmitFallback(element, stateName);
            }

            return;
        }

        if (!CanRenderElementDirectly(element))
        {
            EmitFallback(element, stateName);
            return;
        }

        AppendStaticMarkup(stateName, "<" + element.Tag);
        EmitElementAttributes(element, stateName, selectModel);
        AppendStaticMarkup(stateName, ">");

        if (element.Namespace == ElementNamespace.Html
            && CompilerDomKnowledge.IsVoidTag(element.Tag))
        {
            return;
        }

        ElementContentOverride? contentOverride = FindContentOverride(element);
        DirectiveNode? model = FindDirective(element, "model");
        CodeExpression? textAreaValue = model?.Expression is { } textAreaModel
            ? EmitExpression(textAreaModel)
            : FindStaticPropertyValue(element, "value")?.Value;
        if (contentOverride is not null)
        {
            if (contentOverride.IsRaw)
            {
                AppendStateExpression(
                    stateName,
                    QualifiedCall(
                        CoreNamespace + ".DisplayStringFormatter.ToDisplayString",
                        contentOverride.Value));
            }
            else
            {
                AppendStateHelper(stateName, "SsrInterpolate", contentOverride.Value);
            }
        }
        else if (string.Equals(element.Tag, "textarea", StringComparison.OrdinalIgnoreCase)
            && textAreaValue is not null)
        {
            AppendStateHelper(stateName, "SsrInterpolate", textAreaValue);
        }
        else
        {
            CodeExpression? childSelectModel = string.Equals(
                    element.Tag,
                    "select",
                    StringComparison.OrdinalIgnoreCase)
                && model?.Expression is { } selectExpression
                    ? EmitExpression(selectExpression)
                    : selectModel;
            EmitChildren(ChildrenOf(element), stateName, childSelectModel);
        }

        AppendStaticMarkup(stateName, "</" + element.Tag + ">");
    }

    private void EmitElementAttributes(
        ElementNode element,
        string stateName,
        CodeExpression? selectModel)
    {
        DirectiveNode? model = FindDirective(element, "model");
        DirectiveNode? show = FindDirective(element, "show");
        ElementValue? style = FindStaticPropertyValue(element, "style");

        for (int index = 0; index < element.Properties.Count; index++)
        {
            PropertyNode property = element.Properties[index];
            switch (property)
            {
                case AttributeNode attribute:
                    if (string.Equals(attribute.Name, "style", StringComparison.Ordinal))
                    {
                        EmitStyleAttribute(stateName, style, show);
                        continue;
                    }

                    if (ShouldSkipSerializedProperty(element, attribute.Name, model))
                    {
                        continue;
                    }

                    EmitNamedAttribute(
                        stateName,
                        element.Tag,
                        attribute.Name,
                        CodeExpression.Literal(
                            FrameRenderCodeWriter.StringLiteral(attribute.Value?.Content ?? string.Empty)));
                    break;
                case DirectiveNode directive when directive.Name == "bind":
                    if (directive.Argument is not SimpleExpressionNode { IsStatic: true } argument
                        || directive.Expression is null
                        || HasModifier(directive, "prop"))
                    {
                        continue;
                    }

                    if (string.Equals(argument.Content, "style", StringComparison.Ordinal))
                    {
                        EmitStyleAttribute(stateName, style, show);
                        continue;
                    }

                    if (ShouldSkipSerializedProperty(element, argument.Content, model))
                    {
                        continue;
                    }

                    EmitNamedAttribute(
                        stateName,
                        element.Tag,
                        argument.Content,
                        EmitExpression(directive.Expression));
                    break;
                case DirectiveNode directive when directive.Name == "show":
                    if (style is null && ReferenceEquals(directive, show))
                    {
                        EmitStyleAttribute(stateName, style: null, show: show);
                    }

                    break;
                case DirectiveNode directive when directive.Name == "model":
                    if (ReferenceEquals(directive, model))
                    {
                        EmitModelAttribute(element, stateName, model);
                    }

                    break;
            }
        }

        if (selectModel is not null
            && string.Equals(element.Tag, "option", StringComparison.OrdinalIgnoreCase))
        {
            ElementValue? optionValue = FindStaticPropertyValue(element, "value");
            CodeExpression candidate = optionValue?.Value
                ?? OptionTextValue(element);
            EmitSelectionAttribute(
                stateName,
                element.Tag,
                "selected",
                selectModel,
                candidate,
                booleanModelIsSelection: false);
        }

        if (!string.IsNullOrEmpty(result.ScopeId))
        {
            EmitNamedAttribute(
                stateName,
                element.Tag,
                result.ScopeId!,
                CodeExpression.Literal("string.Empty"));
        }
    }

    private void EmitStyleAttribute(
        string stateName,
        ElementValue? style,
        DirectiveNode? show)
    {
        if (style is null && show?.Expression is null)
        {
            return;
        }

        if (style is null)
        {
            FlushStaticMarkup();
            BeginLine();
            Push("if (!");
            AppendExpression(QualifiedCall(
                ServerNamespace + ".ServerRender.IsTruthy",
                EmitExpression(show!.Expression!)));
            Push(")");
            EndLine();
            AppendLine("{");
            indentLevel++;
            AppendLine(stateName + ".Push(\" style=\\\"display:none;\\\"\");");
            indentLevel--;
            AppendLine("}");
            return;
        }

        AppendStaticMarkup(stateName, " style=\"");
        AppendStateHelper(stateName, "SsrRenderStyle", style.Value);
        if (show?.Expression is not null)
        {
            FlushStaticMarkup();
            BeginLine();
            Push("if (!");
            AppendExpression(QualifiedCall(
                ServerNamespace + ".ServerRender.IsTruthy",
                EmitExpression(show.Expression)));
            Push(")");
            EndLine();
            AppendLine("{");
            indentLevel++;
            AppendLine(stateName + ".Push(\"display:none;\");");
            indentLevel--;
            AppendLine("}");
        }

        AppendStaticMarkup(stateName, "\"");
    }

    private void EmitModelAttribute(
        ElementNode element,
        string stateName,
        DirectiveNode? model)
    {
        if (model?.Expression is null)
        {
            return;
        }

        CodeExpression modelValue = EmitExpression(model.Expression);
        if (string.Equals(element.Tag, "input", StringComparison.OrdinalIgnoreCase))
        {
            string inputType = StaticAttributeValue(element, "type") ?? "text";
            if (string.Equals(inputType, "checkbox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(inputType, "radio", StringComparison.OrdinalIgnoreCase))
            {
                ElementValue? candidateValue = FindStaticPropertyValue(element, "value");
                EmitSelectionAttribute(
                    stateName,
                    element.Tag,
                    "checked",
                    modelValue,
                    candidateValue?.Value
                        ?? CodeExpression.Literal(FrameRenderCodeWriter.StringLiteral("on")),
                    booleanModelIsSelection: string.Equals(
                        inputType,
                        "checkbox",
                        StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                EmitNamedAttribute(stateName, element.Tag, "value", modelValue);
            }
        }
    }

    private void EmitSelectionAttribute(
        string stateName,
        string tag,
        string name,
        CodeExpression modelValue,
        CodeExpression candidate,
        bool booleanModelIsSelection)
    {
        var selected = new CodeExpression();
        selected.Append(ServerNamespace);
        selected.Append(".ServerRender.IsModelValueSelected(");
        selected.Append(modelValue);
        selected.Append(", ");
        selected.Append(candidate);
        selected.Append(booleanModelIsSelection ? ", true)" : ", false)");
        EmitNamedAttribute(stateName, tag, name, selected);
    }

    private void EmitNamedAttribute(
        string stateName,
        string tag,
        string name,
        CodeExpression value)
    {
        if (string.Equals(name, "class", StringComparison.Ordinal))
        {
            AppendStaticMarkup(stateName, " class=\"");
            AppendStateHelper(stateName, "SsrRenderClass", value);
            AppendStaticMarkup(stateName, "\"");
            return;
        }

        FlushStaticMarkup();
        BeginLine();
        Push(stateName);
        Push(".Push(");
        Push(ServerNamespace);
        Push(".ServerRender.SsrRenderDynamicAttribute(");
        Push(FrameRenderCodeWriter.StringLiteral(name));
        Push(", ");
        AppendExpression(value);
        Push(", ");
        Push(FrameRenderCodeWriter.StringLiteral(tag));
        Push("));");
        EndLine();
    }

    private void EmitIf(
        IfNode conditional,
        string stateName,
        CodeExpression? selectModel)
    {
        FlushStaticMarkup();
        bool hasElse = false;
        for (int index = 0; index < conditional.Branches.Count; index++)
        {
            IfBranchNode branch = conditional.Branches[index];
            if (branch.Condition is null)
            {
                AppendLine(index == 0 ? "if (true)" : "else");
                hasElse = true;
            }
            else
            {
                BeginLine();
                Push(index == 0 ? "if (" : "else if (");
                AppendExpression(EmitExpression(branch.Condition));
                Push(")");
                EndLine();
            }

            AppendLine("{");
            indentLevel++;
            EmitChildrenAsRoot(branch.Children, stateName, selectModel);
            FlushStaticMarkup();
            indentLevel--;
            AppendLine("}");
        }

        if (!hasElse)
        {
            AppendLine("else");
            AppendLine("{");
            indentLevel++;
            AppendStateHelper(
                stateName,
                "SsrRenderComment",
                CodeExpression.Literal(FrameRenderCodeWriter.StringLiteral("v-if")));
            FlushStaticMarkup();
            indentLevel--;
            AppendLine("}");
        }
    }

    private void EmitFor(
        ForNode loop,
        string stateName,
        CodeExpression? selectModel)
    {
        AppendMarker(stateName, "FragmentStart");
        FlushStaticMarkup();
        string sourceName = NextName("listSource");
        string itemName = NextName("listItem");
        string indexName = NextName("listIndex");

        BeginLine();
        Push("var ");
        Push(sourceName);
        Push(" = ");
        AppendExpression(EmitExpression(loop.Source));
        Push(";");
        EndLine();
        AppendLine("int " + indexName + " = 0;");
        AppendLine("foreach (var " + itemName + " in " + sourceName + ")");
        AppendLine("{");
        indentLevel++;
        AppendForAliases(loop, itemName, indexName);
        EmitChildrenAsRoot(loop.Children, stateName, selectModel);
        FlushStaticMarkup();
        AppendLine(indexName + "++;");
        indentLevel--;
        AppendLine("}");
        AppendMarker(stateName, "FragmentEnd");
    }

    private void AppendForAliases(ForNode loop, string itemName, string indexName)
    {
        if (loop.ValueAlias is null)
        {
            return;
        }

        string valueName = ParameterName(loop.ValueAlias, "item");
        if (loop.ObjectIndexAlias is not null)
        {
            AppendLine("var " + valueName + " = " + itemName + ".Value;");
            AppendLine(
                "var " + ParameterName(loop.KeyAlias, "key") + " = " + itemName + ".Key;");
            AppendLine(
                "var " + ParameterName(loop.ObjectIndexAlias, "index") + " = " + indexName + ";");
            return;
        }

        AppendLine("var " + valueName + " = " + itemName + ";");
        if (loop.KeyAlias is not null)
        {
            AppendLine(
                "var " + ParameterName(loop.KeyAlias, "index") + " = " + indexName + ";");
        }
    }

    private void EmitTeleport(ElementNode element, string stateName)
    {
        ElementValue? target = FindStaticPropertyValue(element, "to");
        ElementValue? disabled = FindStaticPropertyValue(element, "disabled");
        FlushStaticMarkup();
        string teleportState = NextName("teleportState");
        BeginLine();
        Push("await ");
        Push(ServerNamespace);
        Push(".ServerRender.SsrRenderTeleportAsync(");
        Push(stateName);
        Push(", async ");
        Push(teleportState);
        Push(" =>");
        EndLine();
        AppendLine("{");
        indentLevel++;
        EmitChildren(ChildrenOf(element), teleportState, selectModel: null);
        FlushStaticMarkup();
        AppendLine("await global::System.Threading.Tasks.Task.CompletedTask;");
        indentLevel--;
        AppendLine("},");
        BeginLine();
        Push("global::System.Convert.ToString(");
        AppendExpression(target?.Value ?? CodeExpression.Literal("null"));
        Push(", global::System.Globalization.CultureInfo.InvariantCulture),");
        EndLine();
        BeginLine();
        AppendExpression(disabled is null
            ? CodeExpression.Literal("false")
            : QualifiedCall(ServerNamespace + ".ServerRender.IsTruthy", disabled.Value));
        Push(");");
        EndLine();
    }

    private void EmitSuspense(ElementNode element, string stateName)
    {
        FlushStaticMarkup();
        string suspenseState = NextName("suspenseState");
        BeginLine();
        Push("await ");
        Push(ServerNamespace);
        Push(".ServerRender.SsrRenderSuspenseAsync(");
        Push(stateName);
        Push(", async ");
        Push(suspenseState);
        Push(" =>");
        EndLine();
        AppendLine("{");
        indentLevel++;
        EmitChildrenAsRoot(DefaultContentOf(element), suspenseState, selectModel: null);
        FlushStaticMarkup();
        AppendLine("await global::System.Threading.Tasks.Task.CompletedTask;");
        indentLevel--;
        AppendLine("});");
    }

    private void EmitFallback(TemplateChildNode node, string stateName)
    {
        FlushStaticMarkup();
        string fallbackName = NextName("RenderFallback");
        AppendLine(ComponentsNamespace + ".VirtualNode? " + fallbackName + "()");
        AppendLine("{");
        indentLevel++;
        var fallbackWriter = new FrameRenderCodeWriter(result, indentLevel, indentText);
        string fallbackCode = fallbackWriter.EmitRenderBody(node);
        int fallbackOffset = builder.Length;
        builder.Append(fallbackCode);
        for (int index = 0; index < fallbackWriter.SourceMappings.Count; index++)
        {
            RenderSourceMapping mapping = fallbackWriter.SourceMappings[index];
            mappingSites.Add((
                fallbackOffset + OffsetAtLineColumn(
                    fallbackCode,
                    mapping.GeneratedLine,
                    mapping.GeneratedColumn),
                mapping.TemplateLocation));
        }
        indentLevel--;
        AppendLine("}");
        BeginLine();
        Push("await ");
        Push(ServerNamespace);
        Push(".ServerRender.SsrRenderComponentAsync(");
        Push(stateName);
        Push(", ");
        Push(fallbackName);
        Push("(), parent);");
        EndLine();
    }

    private bool CanRenderElementDirectly(ElementNode element)
    {
        int styleCount = 0;
        for (int index = 0; index < element.Properties.Count; index++)
        {
            if (element.Properties[index] is AttributeNode attribute)
            {
                if (string.Equals(attribute.Name, "style", StringComparison.Ordinal))
                {
                    styleCount++;
                }

                continue;
            }

            if (element.Properties[index] is not DirectiveNode directive)
            {
                continue;
            }

            if (directive.Name == "bind")
            {
                if (directive.Argument is not SimpleExpressionNode { IsStatic: true } argument)
                {
                    return false;
                }

                if (HasModifier(directive, "prop"))
                {
                    return false;
                }

                if (string.Equals(argument.Content, "style", StringComparison.Ordinal))
                {
                    styleCount++;
                }
            }

            if (directive.Name == "model"
                && string.Equals(element.Tag, "input", StringComparison.OrdinalIgnoreCase)
                && FindStaticPropertyValue(element, "type") is { IsStatic: false })
            {
                return false;
            }
        }

        return styleCount <= 1;
    }

    private ElementContentOverride? FindContentOverride(ElementNode element)
    {
        ElementContentOverride? resultOverride = null;
        for (int index = 0; index < element.Properties.Count; index++)
        {
            PropertyNode property = element.Properties[index];
            if (property is AttributeNode attribute
                && IsContentOverrideName(attribute.Name))
            {
                resultOverride = new ElementContentOverride(
                    CodeExpression.Literal(FrameRenderCodeWriter.StringLiteral(
                        attribute.Value?.Content ?? string.Empty)),
                    string.Equals(attribute.Name, "innerHTML", StringComparison.Ordinal));
            }
            else if (property is DirectiveNode directive)
            {
                if (directive.Name == "html" && directive.Expression is not null)
                {
                    resultOverride = new ElementContentOverride(
                        EmitExpression(directive.Expression),
                        isRaw: true);
                }
                else if (directive.Name == "text" && directive.Expression is not null)
                {
                    resultOverride = new ElementContentOverride(
                        EmitExpression(directive.Expression),
                        isRaw: false);
                }
                else if (directive.Name == "bind"
                    && directive.Argument is SimpleExpressionNode { IsStatic: true } argument
                    && IsContentOverrideName(argument.Content)
                    && directive.Expression is not null)
                {
                    resultOverride = new ElementContentOverride(
                        EmitExpression(directive.Expression),
                        string.Equals(argument.Content, "innerHTML", StringComparison.Ordinal));
                }
            }
        }

        return resultOverride;
    }

    private ElementValue? FindStaticPropertyValue(ElementNode element, string name)
    {
        ElementValue? value = null;
        for (int index = 0; index < element.Properties.Count; index++)
        {
            PropertyNode property = element.Properties[index];
            if (property is AttributeNode attribute
                && string.Equals(attribute.Name, name, StringComparison.Ordinal))
            {
                value = new ElementValue(
                    CodeExpression.Literal(FrameRenderCodeWriter.StringLiteral(
                        attribute.Value?.Content ?? string.Empty)),
                    isStatic: true);
            }
            else if (property is DirectiveNode { Name: "bind" } directive
                && directive.Argument is SimpleExpressionNode { IsStatic: true } argument
                && string.Equals(argument.Content, name, StringComparison.Ordinal)
                && directive.Expression is not null)
            {
                value = new ElementValue(EmitExpression(directive.Expression), isStatic: false);
            }
        }

        return value;
    }

    private static DirectiveNode? FindDirective(ElementNode element, string name)
    {
        for (int index = element.Properties.Count - 1; index >= 0; index--)
        {
            if (element.Properties[index] is DirectiveNode directive
                && string.Equals(directive.Name, name, StringComparison.Ordinal))
            {
                return directive;
            }
        }

        return null;
    }

    private static bool ShouldSkipSerializedProperty(
        ElementNode element,
        string name,
        DirectiveNode? model)
    {
        if (name is "key" or "ref" or "innerHTML" or "textContent")
        {
            return true;
        }

        if (string.Equals(element.Tag, "textarea", StringComparison.OrdinalIgnoreCase)
            && string.Equals(name, "value", StringComparison.Ordinal))
        {
            return true;
        }

        if (model is not null
            && string.Equals(element.Tag, "input", StringComparison.OrdinalIgnoreCase))
        {
            if (name == "checked")
            {
                return true;
            }

            if (name == "value" && !IsCheckableInput(element))
            {
                return true;
            }
        }

        return name.Length > 2
            && name[0] == 'o'
            && name[1] == 'n'
            && !char.IsLower(name[2]);
    }

    private static bool IsContentOverrideName(string name) =>
        name is "innerHTML" or "textContent";

    private static bool IsCheckableInput(ElementNode element)
    {
        string? inputType = StaticAttributeValue(element, "type");
        return string.Equals(inputType, "checkbox", StringComparison.OrdinalIgnoreCase)
            || string.Equals(inputType, "radio", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasModifier(DirectiveNode directive, string name)
    {
        for (int index = 0; index < directive.Modifiers.Count; index++)
        {
            if (string.Equals(directive.Modifiers[index].Content, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? StaticAttributeValue(ElementNode element, string name)
    {
        for (int index = element.Properties.Count - 1; index >= 0; index--)
        {
            if (element.Properties[index] is AttributeNode attribute
                && string.Equals(attribute.Name, name, StringComparison.Ordinal))
            {
                return attribute.Value?.Content ?? string.Empty;
            }
        }

        return null;
    }

    private CodeExpression OptionTextValue(ElementNode element)
    {
        IReadOnlyList<TemplateChildNode> children = ChildrenOf(element);
        var parts = new List<CodeExpression>();
        for (int index = 0; index < children.Count; index++)
        {
            switch (children[index])
            {
                case TextNode text:
                    parts.Add(CodeExpression.Literal(
                        FrameRenderCodeWriter.StringLiteral(text.Content)));
                    break;
                case InterpolationNode interpolation:
                    parts.Add(QualifiedCall(
                        CoreNamespace + ".DisplayStringFormatter.ToDisplayString",
                        EmitExpression(interpolation.Content)));
                    break;
                case TextCallNode textCall:
                    parts.Add(QualifiedCall(
                        CoreNamespace + ".DisplayStringFormatter.ToDisplayString",
                        EmitExpression(textCall.Content)));
                    break;
            }
        }

        if (parts.Count == 0)
        {
            return CodeExpression.Literal("string.Empty");
        }

        if (parts.Count == 1)
        {
            return parts[0];
        }

        var combined = new CodeExpression();
        combined.Append("(");
        for (int index = 0; index < parts.Count; index++)
        {
            if (index > 0)
            {
                combined.Append(" + ");
            }

            combined.Append(parts[index]);
        }

        combined.Append(")");
        return combined;
    }

    private IReadOnlyList<TemplateChildNode> ChildrenOf(ElementNode element) =>
        result.GetTransformedChildren(element);

    private IReadOnlyList<TemplateChildNode> DefaultContentOf(ElementNode element)
    {
        IReadOnlyList<TemplateChildNode> children = ChildrenOf(element);
        ElementNode? namedDefault = null;
        bool hasSlotTemplate = false;
        for (int index = 0; index < children.Count; index++)
        {
            if (children[index] is not ElementNode
                {
                    ElementType: ElementType.Template,
                } template)
            {
                continue;
            }

            DirectiveNode? slot = FindDirective(template, "slot");
            if (slot is null)
            {
                continue;
            }

            hasSlotTemplate = true;
            if (slot.Argument is null
                || slot.Argument is SimpleExpressionNode
                {
                    IsStatic: true,
                    Content: "default",
                })
            {
                namedDefault = template;
                break;
            }
        }

        if (namedDefault is not null)
        {
            return ChildrenOf(namedDefault);
        }

        if (!hasSlotTemplate)
        {
            return children;
        }

        var implicitDefault = new List<TemplateChildNode>();
        for (int index = 0; index < children.Count; index++)
        {
            TemplateChildNode child = children[index];
            if (child is CommentNode)
            {
                continue;
            }

            if (child is ElementNode
                {
                    ElementType: ElementType.Template,
                } template
                && FindDirective(template, "slot") is not null)
            {
                continue;
            }

            implicitDefault.Add(child);
        }

        return implicitDefault;
    }

    private static bool IsTeleport(string tag) =>
        tag is "Teleport" or "teleport";

    private static bool IsSuspense(string tag) =>
        tag is "Suspense" or "suspense";

    private static bool IsKeepAlive(string tag) =>
        tag is "KeepAlive" or "keep-alive";

    private static bool IsTransition(string tag) =>
        tag is "Transition" or "transition" or "BaseTransition" or "base-transition";

    private CodeExpression EmitExpression(object? node)
    {
        switch (node)
        {
            case null:
                return CodeExpression.Literal("null");
            case string raw:
                return CodeExpression.Literal(FrameRenderCodeWriter.MapRawLiteral(raw));
            case TextNode text:
                return CodeExpression.Literal(FrameRenderCodeWriter.StringLiteral(text.Content));
            case SimpleExpressionNode simple:
                return EmitSimpleExpression(simple);
            case InterpolationNode interpolation:
                return EmitExpression(interpolation.Content);
            case CompoundExpressionNode compound:
                var compoundExpression = new CodeExpression();
                for (int index = 0; index < compound.Parts.Count; index++)
                {
                    object part = compound.Parts[index];
                    compoundExpression.Append(part is string partText
                        ? CodeExpression.Literal(FrameRenderCodeWriter.MapRawLiteral(partText))
                        : EmitExpression(part));
                }

                return compoundExpression;
            default:
                throw new InvalidOperationException(
                    "Unsupported direct server expression '" + node.GetType().Name + "'.");
        }
    }

    private CodeExpression EmitSimpleExpression(SimpleExpressionNode simple)
    {
        if (simple.IsStatic)
        {
            return CodeExpression.Literal(FrameRenderCodeWriter.StringLiteral(simple.Content));
        }

        string text = FrameRenderCodeWriter.MapRawLiteral(simple.Content);
        CodeExpression expression = CodeExpression.Literal(text);
        string source = simple.Location.Source;
        if (!string.IsNullOrEmpty(source))
        {
            int sourceOffset = text.IndexOf(source, StringComparison.Ordinal);
            if (sourceOffset >= 0)
            {
                expression.AddMapping(sourceOffset, simple.Location);
            }
        }

        return expression;
    }

    private static CodeExpression QualifiedCall(string name, CodeExpression argument)
    {
        var expression = new CodeExpression();
        expression.Append(name);
        expression.Append("(");
        expression.Append(argument);
        expression.Append(")");
        return expression;
    }

    private void AppendStateHelper(
        string stateName,
        string helperName,
        CodeExpression value)
    {
        AppendStateExpression(
            stateName,
            QualifiedCall(ServerNamespace + ".ServerRender." + helperName, value));
    }

    private void AppendStateExpression(string stateName, CodeExpression value)
    {
        FlushStaticMarkup();
        BeginLine();
        Push(stateName);
        Push(".Push(");
        AppendExpression(value);
        Push(");");
        EndLine();
    }

    private void AppendMarker(string stateName, string markerName)
    {
        FlushStaticMarkup();
        AppendLine(
            stateName + ".Push(" + CoreNamespace + ".HydrationMarkers." + markerName + ");");
    }

    private void AppendStaticMarkup(string stateName, string value)
    {
        if (!string.Equals(staticStateName, stateName, StringComparison.Ordinal))
        {
            FlushStaticMarkup();
            staticStateName = stateName;
        }

        staticMarkup.Append(value);
    }

    private void FlushStaticMarkup()
    {
        if (staticMarkup.Length == 0)
        {
            return;
        }

        AppendLine(
            staticStateName + ".Push(" + FrameRenderCodeWriter.StringLiteral(staticMarkup.ToString()) + ");");
        staticMarkup.Clear();
    }

    private static string EscapeHtml(string value)
    {
        StringBuilder? escaped = null;
        int copyStart = 0;
        for (int index = 0; index < value.Length; index++)
        {
            string? entity = value[index] switch
            {
                '"' => "&quot;",
                '&' => "&amp;",
                '\'' => "&#39;",
                '<' => "&lt;",
                '>' => "&gt;",
                _ => null,
            };
            if (entity is null)
            {
                continue;
            }

            escaped ??= new StringBuilder(value.Length + 16);
            escaped.Append(value, copyStart, index - copyStart);
            escaped.Append(entity);
            copyStart = index + 1;
        }

        if (escaped is null)
        {
            return value;
        }

        escaped.Append(value, copyStart, value.Length - copyStart);
        return escaped.ToString();
    }

    private static string ParameterName(ExpressionNode? expression, string fallback)
    {
        if (expression is not SimpleExpressionNode simple)
        {
            return fallback;
        }

        string name = simple.Content;
        return string.IsNullOrEmpty(name) || name[0] == '_'
            ? fallback
            : name;
    }

    private string NextName(string prefix)
    {
        string name = prefix + generatedNameIndex.ToString(CultureInfo.InvariantCulture);
        generatedNameIndex++;
        return name;
    }

    private void AppendExpression(CodeExpression expression)
    {
        for (int index = 0; index < expression.Mappings.Count; index++)
        {
            (int offset, SourceLocation location) = expression.Mappings[index];
            mappingSites.Add((builder.Length + offset, location));
        }

        builder.Append(expression.Text);
    }

    private void AppendLine(string text)
    {
        BeginLine();
        Push(text);
        EndLine();
    }

    private void BeginLine()
    {
        for (int level = 0; level < indentLevel; level++)
        {
            builder.Append(indentText);
        }
    }

    private void EndLine() => builder.Append('\n');

    private void Push(string text) => builder.Append(text);

    private IReadOnlyList<RenderSourceMapping> BuildSourceMappings(string code)
    {
        if (mappingSites.Count == 0)
        {
            return Array.Empty<RenderSourceMapping>();
        }

        var mappings = new List<RenderSourceMapping>(mappingSites.Count);
        for (int index = 0; index < mappingSites.Count; index++)
        {
            (int offset, SourceLocation location) = mappingSites[index];
            (int line, int column) = LineColumnAt(code, offset);
            mappings.Add(new RenderSourceMapping
            {
                GeneratedLine = line,
                GeneratedColumn = column,
                TemplateLocation = location,
            });
        }

        return mappings;
    }

    private static (int Line, int Column) LineColumnAt(string text, int offset)
    {
        int line = 0;
        int lineStart = 0;
        int limit = offset < text.Length ? offset : text.Length;
        for (int index = 0; index < limit; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }

        return (line, offset - lineStart);
    }

    private static int OffsetAtLineColumn(string text, int line, int column)
    {
        int currentLine = 0;
        int offset = 0;
        while (currentLine < line && offset < text.Length)
        {
            if (text[offset] == '\n')
            {
                currentLine++;
            }

            offset++;
        }

        return Math.Min(offset + column, text.Length);
    }

    private sealed class ElementContentOverride
    {
        internal ElementContentOverride(CodeExpression value, bool isRaw)
        {
            Value = value;
            IsRaw = isRaw;
        }

        internal CodeExpression Value { get; }

        internal bool IsRaw { get; }
    }

    private sealed class ElementValue
    {
        internal ElementValue(CodeExpression value, bool isStatic)
        {
            Value = value;
            IsStatic = isStatic;
        }

        internal CodeExpression Value { get; }

        internal bool IsStatic { get; }
    }

    private sealed class CodeExpression
    {
        private readonly StringBuilder text = new();
        private readonly List<(int Offset, SourceLocation Location)> mappings = new();

        internal string Text => text.ToString();

        internal IReadOnlyList<(int Offset, SourceLocation Location)> Mappings => mappings;

        internal static CodeExpression Literal(string value)
        {
            var expression = new CodeExpression();
            expression.Append(value);
            return expression;
        }

        internal void Append(string value) => text.Append(value);

        internal void Append(CodeExpression expression)
        {
            int offset = text.Length;
            text.Append(expression.Text);
            for (int index = 0; index < expression.mappings.Count; index++)
            {
                (int mappingOffset, SourceLocation location) = expression.mappings[index];
                mappings.Add((offset + mappingOffset, location));
            }
        }

        internal void AddMapping(int offset, SourceLocation location) =>
            mappings.Add((offset, location));
    }
}
