using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Microsoft.CodeAnalysis.CSharp;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Serializes the template code-generation tree into the statement-form component model adopted by
/// <c>[V01.01.15.02]</c>. The writer names runtime types only in emitted text; the template compiler
/// remains a runtime-reference-free <c>netstandard2.0</c> analyzer dependency.
/// </summary>
/// <remarks>
/// Each block is emitted as an ordered <c>OpenBlock</c>, descendant construction and tracking,
/// <c>CloseBlock</c> sequence. Node construction uses the closed public node algebra directly.
/// ObjectProperty sets, lists, and slots dissolve into dictionaries, loops, and closures in the consumer
/// compilation. Specified by <c>[SFC-CG-1]</c>, <c>[SFC-CG-2]</c>, and <c>[RND-BLOCK-3]</c>.
/// </remarks>
internal sealed class FrameRenderCodeWriter
{
    private const string ComponentsNamespace = "global::Assimalign.Viu.Components";
    private const string CoreNamespace = "global::Assimalign.Viu";
    private const string BrowserNamespace = "global::Assimalign.Viu.Browser";
    private const string GeneratedNamespace = "global::Assimalign.Viu.Generated";

    private readonly TransformResult result;
    private readonly StringBuilder builder = new();
    private readonly string indentText;
    private readonly List<(int Offset, SourceLocation Location)> mappingSites = new();
    private IReadOnlyList<RenderSourceMapping> sourceMappings = Array.Empty<RenderSourceMapping>();
    private int indentLevel;
    private int generatedNameIndex;

    /// <summary>The source map produced by the most recent <see cref="EmitRenderBody"/> call.</summary>
    public IReadOnlyList<RenderSourceMapping> SourceMappings => sourceMappings;

    /// <summary>Initializes a writer for one transformed template.</summary>
    public FrameRenderCodeWriter(
        TransformResult result,
        int indentLevel,
        string indentText)
    {
        this.result = result;
        this.indentLevel = indentLevel;
        this.indentText = indentText;
    }

    /// <summary>Emits one frame-taking render-method body.</summary>
    public string EmitRenderBody()
    {
        CodeExpression root = result.CodegenNode is null
            ? CodeExpression.Literal("null")
            : EmitVirtualValue(result.CodegenNode);

        BeginLine();
        Push("return ");
        AppendExpression(root);
        Push(";");
        EndLine();

        string code = builder.ToString();
        sourceMappings = BuildSourceMappings(code);
        return code;
    }

    private CodeExpression EmitVirtualValue(object? node)
    {
        switch (node)
        {
            case null:
                return CodeExpression.Literal("null");
            case ElementNode element:
                return EmitVirtualValue(result.GetCodegenNode(element));
            case IfNode ifNode:
                return EmitVirtualValue(ifNode.CodegenNode);
            case ForNode forNode:
                return EmitVirtualValue(forNode.CodegenNode);
            case TextCallNode textCall:
                return EmitVirtualValue(textCall.CodegenNode);
            case TextNode text:
                return EmitTextNode(CodeExpression.Literal(StringLiteral(text.Content)), 0);
            case InterpolationNode interpolation:
                return EmitTextNode(EmitDisplayString(interpolation.Content), (int)PatchFlags.Text);
            case CompoundExpressionNode compound:
                return EmitTextNode(EmitCompoundExpression(compound), (int)PatchFlags.Text);
            case CommentNode comment:
                return EmitCommentNode(StringLiteral(comment.Content));
            case VirtualNodeCall virtualNode:
                return EmitVirtualNode(virtualNode);
            case CallExpression call:
                return EmitCallAsVirtualValue(call);
            case ConditionalExpression conditional:
                return EmitConditionalVirtualValue(conditional);
            case CacheExpression cache:
                return EmitCachedVirtualValue(cache);
            case SyntaxList<TemplateChildNode> children:
                return EmitRootFromChildren(children);
            case SimpleExpressionNode simple:
                return simple.IsStatic
                    ? EmitTextNode(CodeExpression.Literal(StringLiteral(simple.Content)), 0)
                    : EmitTextNode(EmitDisplayString(simple), (int)PatchFlags.Text);
            case string raw when raw is "undefined" or "void 0":
                return CodeExpression.Literal("null");
            case string raw:
                return EmitTextNode(CodeExpression.Literal(MapRawLiteral(raw)), 0);
            default:
                throw new InvalidOperationException(
                    $"Unsupported virtual-value node '{node.GetType().Name}'.");
        }
    }

    private CodeExpression EmitVirtualNode(VirtualNodeCall node)
    {
        if (node.IsBlock)
        {
            AppendStatement(node.DisableTracking
                ? "frame.OpenBlock(true);"
                : "frame.OpenBlock();");
        }

        VirtualNodeTag tag = ClassifyTag(node.Tag);
        return tag.Kind switch
        {
            VirtualNodeTagKind.Element => EmitElementNode(node, tag),
            VirtualNodeTagKind.Fragment => EmitFragmentNode(node),
            VirtualNodeTagKind.Teleport => EmitTeleportNode(node),
            VirtualNodeTagKind.KeepAlive => EmitStructuralComponentNode(node, "KeepAliveNode"),
            VirtualNodeTagKind.Suspense => EmitStructuralComponentNode(node, "SuspenseNode"),
            VirtualNodeTagKind.Transition => EmitStructuralComponentNode(node, "TransitionNode"),
            VirtualNodeTagKind.Component => EmitComponentNode(node, tag),
            VirtualNodeTagKind.Dynamic => EmitDynamicNode(node, tag),
            _ => throw new InvalidOperationException("Unsupported virtual-node tag classification."),
        };
    }

    private CodeExpression EmitElementNode(VirtualNodeCall node, VirtualNodeTag tag)
    {
        ElementPropertyEmission properties = EmitElementProperties(node.Properties);
        CodeExpression children = EmitChildren(node.Children);
        CodeExpression directives = EmitDirectiveInvocations(node.Directives);
        CodeExpression dynamicBindingIndices = EmitDynamicBindingIndices(
            properties.BindingsName,
            ParseDynamicPropertyNames(node.DynamicProperties));
        CodeExpression renderPlan = EmitRenderPlan(node, dynamicBindingIndices);
        string nodeName = NextName("node");

        BeginLine();
        Push("var ");
        Push(nodeName);
        Push(" = new ");
        Push(ComponentsNamespace);
        Push(".ElementNode(new ");
        Push(ComponentsNamespace);
        Push(".QualifiedName(");
        AppendExpression(tag.Value);
        Push("), bindings: ");
        Push(properties.BindingsName);
        Push(", children: ");
        AppendExpression(children);
        Push(", directives: ");
        AppendExpression(directives);
        Push(", key: ");
        Push(properties.KeyName);
        Push(", mountReference: ");
        Push(properties.MountReferenceName);
        Push(", renderPlan: ");
        AppendExpression(renderPlan);
        Push(");");
        EndLine();

        TrackIfRequired(node, nodeName);
        return CodeExpression.Literal(nodeName);
    }

    private CodeExpression EmitFragmentNode(VirtualNodeCall node)
    {
        GenericPropertyEmission properties = EmitGenericProperties(node.Properties, PropertyValuePurpose.Generic);
        CodeExpression children = EmitChildren(node.Children);
        CodeExpression renderPlan = EmitRenderPlan(node, CodeExpression.Literal("null"));
        string nodeName = NextName("node");

        BeginLine();
        Push("var ");
        Push(nodeName);
        Push(" = new ");
        Push(ComponentsNamespace);
        Push(".FragmentNode(");
        AppendExpression(children);
        Push(", key: ");
        Push(properties.KeyName);
        Push(", renderPlan: ");
        AppendExpression(renderPlan);
        Push(");");
        EndLine();

        TrackIfRequired(node, nodeName);
        return CodeExpression.Literal(nodeName);
    }

    private CodeExpression EmitTeleportNode(VirtualNodeCall node)
    {
        GenericPropertyEmission properties = EmitGenericProperties(node.Properties, PropertyValuePurpose.Generic);
        CodeExpression children = EmitChildren(node.Children);
        CodeExpression renderPlan = EmitRenderPlan(node, CodeExpression.Literal("null"));
        string targetName = NextName("targetIdentifier");
        string disabledName = NextName("isDisabled");
        string deferredName = NextName("isDeferred");
        string valueName = NextName("propertyValue");

        AppendStatement(
            $"string {targetName} = {properties.PropertiesName}.TryGetValue(\"to\", out object? {valueName})"
            + $" ? global::System.Convert.ToString({valueName}, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty"
            + " : string.Empty;");
        AppendStatement(
            $"bool {disabledName} = {properties.PropertiesName}.TryGetValue(\"disabled\", out {valueName}) && {valueName} is true;");
        AppendStatement(
            $"bool {deferredName} = {properties.PropertiesName}.TryGetValue(\"defer\", out {valueName}) && {valueName} is true;");

        string nodeName = NextName("node");
        BeginLine();
        Push("var ");
        Push(nodeName);
        Push(" = new ");
        Push(ComponentsNamespace);
        Push(".TeleportNode(");
        Push(targetName);
        Push(", children: ");
        AppendExpression(children);
        Push(", isDisabled: ");
        Push(disabledName);
        Push(", isDeferred: ");
        Push(deferredName);
        Push(", key: ");
        Push(properties.KeyName);
        Push(", renderPlan: ");
        AppendExpression(renderPlan);
        Push(");");
        EndLine();

        TrackIfRequired(node, nodeName);
        return CodeExpression.Literal(nodeName);
    }

    private CodeExpression EmitComponentNode(VirtualNodeCall node, VirtualNodeTag tag)
    {
        ComponentInvocationEmission invocation = EmitComponentInvocation(
            node.Properties,
            node.Children,
            node.Directives);
        CodeExpression renderPlan = EmitRenderPlan(node, CodeExpression.Literal("null"));
        string nodeName = NextName("node");

        BeginLine();
        Push("var ");
        Push(nodeName);
        Push(" = new ");
        Push(ComponentsNamespace);
        Push(".ComponentNode(");
        AppendExpression(tag.Value);
        Push(", ");
        Push(invocation.InvocationName);
        Push(", key: ");
        Push(invocation.KeyName);
        Push(", mountReference: ");
        Push(invocation.MountReferenceName);
        Push(", renderPlan: ");
        AppendExpression(renderPlan);
        Push(");");
        EndLine();

        TrackIfRequired(node, nodeName);
        return CodeExpression.Literal(nodeName);
    }

    private CodeExpression EmitStructuralComponentNode(VirtualNodeCall node, string typeName)
    {
        ComponentInvocationEmission invocation = EmitComponentInvocation(
            node.Properties,
            node.Children,
            node.Directives);
        if (node.IsBlock)
        {
            // These structural nodes intentionally carry no RenderPlan in the adopted surface. Close the
            // compiler block so nested assembly stays balanced; their executors own update selection.
            AppendStatement("frame.CloseBlock();");
        }

        string nodeName = NextName("node");
        AppendStatement(
            $"var {nodeName} = new {ComponentsNamespace}.{typeName}({invocation.InvocationName}, {invocation.KeyName});");
        TrackIfRequired(node, nodeName);
        return CodeExpression.Literal(nodeName);
    }

    private CodeExpression EmitDynamicNode(VirtualNodeCall node, VirtualNodeTag tag)
    {
        ElementPropertyEmission elementProperties = EmitElementProperties(node.Properties);
        ComponentInvocationEmission componentInvocation = EmitComponentInvocation(
            node.Properties,
            node.Children,
            node.Directives);
        CodeExpression children = EmitChildren(node.Children);
        CodeExpression directives = EmitDirectiveInvocations(node.Directives);
        CodeExpression renderPlan = EmitRenderPlan(node, CodeExpression.Literal("null"));
        string nodeName = NextName("node");

        BeginLine();
        Push("var ");
        Push(nodeName);
        Push(" = ");
        Push(CoreNamespace);
        Push(".DynamicComponents.Create(");
        AppendExpression(tag.Value);
        Push(", invocation: ");
        Push(componentInvocation.InvocationName);
        Push(", bindings: ");
        Push(elementProperties.BindingsName);
        Push(", children: ");
        AppendExpression(children);
        Push(", directives: ");
        AppendExpression(directives);
        Push(", key: ");
        Push(componentInvocation.KeyName);
        Push(", mountReference: ");
        Push(componentInvocation.MountReferenceName);
        Push(", renderPlan: ");
        AppendExpression(renderPlan);
        Push(");");
        EndLine();

        TrackIfRequired(node, nodeName);
        return CodeExpression.Literal(nodeName);
    }

    private CodeExpression EmitCallAsVirtualValue(CallExpression call)
    {
        if (call.Callee is not RuntimeHelper helper)
        {
            return EmitTextNode(EmitCallExpression(call), (int)PatchFlags.Text);
        }

        if (helper == HelperNames.CreateText)
        {
            CodeExpression text = call.Arguments.Count == 0
                ? CodeExpression.Literal("string.Empty")
                : EmitExpression(call.Arguments[0]);
            int patchFlags = call.Arguments.Count > 1
                ? ParseRawInteger(call.Arguments[1])
                : 0;
            return EmitTextNode(text, patchFlags);
        }

        if (helper == HelperNames.CreateComment)
        {
            string text = call.Arguments.Count == 0
                ? StringLiteral(string.Empty)
                : EmitExpression(call.Arguments[0]).Text;
            return EmitCommentNode(text);
        }

        if (helper == HelperNames.CreateStatic)
        {
            CodeExpression content = call.Arguments.Count == 0
                ? CodeExpression.Literal(StringLiteral(string.Empty))
                : EmitExpression(call.Arguments[0]);
            return EmitStaticNode(content);
        }

        if (helper == HelperNames.RenderSlot)
        {
            return EmitSlotOutlet(call);
        }

        if (helper == HelperNames.WithMemo)
        {
            return EmitMemoizedVirtualValue(call);
        }

        throw new InvalidOperationException(
            $"Helper '{helper.Name}' does not produce a virtual node in this emission context.");
    }

    private CodeExpression EmitConditionalVirtualValue(ConditionalExpression conditional)
    {
        string resultName = NextName("conditionalNode");
        CodeExpression test = EmitExpression(conditional.Test);
        AppendStatement($"{ComponentsNamespace}.VirtualNode? {resultName};");

        BeginLine();
        Push("if (");
        AppendExpression(test);
        Push(")");
        EndLine();
        AppendLine("{");
        indentLevel++;
        CodeExpression consequent = EmitVirtualValue(conditional.Consequent);
        BeginLine();
        Push(resultName);
        Push(" = ");
        AppendExpression(consequent);
        Push(";");
        EndLine();
        indentLevel--;
        AppendLine("}");
        AppendLine("else");
        AppendLine("{");
        indentLevel++;
        CodeExpression alternate = EmitVirtualValue(conditional.Alternate);
        BeginLine();
        Push(resultName);
        Push(" = ");
        AppendExpression(alternate);
        Push(";");
        EndLine();
        indentLevel--;
        AppendLine("}");

        return CodeExpression.Literal(resultName);
    }

    private CodeExpression EmitRootFromChildren(IReadOnlyList<TemplateChildNode> children)
    {
        if (children.Count == 0)
        {
            return CodeExpression.Literal("null");
        }

        if (children.Count == 1)
        {
            return EmitVirtualValue(children[0]);
        }

        CodeExpression childCollection = EmitChildren(children);
        string nodeName = NextName("node");
        BeginLine();
        Push("var ");
        Push(nodeName);
        Push(" = new ");
        Push(ComponentsNamespace);
        Push(".FragmentNode(");
        AppendExpression(childCollection);
        Push(");");
        EndLine();
        return CodeExpression.Literal(nodeName);
    }

    private CodeExpression EmitTextNode(CodeExpression text, int patchFlags)
    {
        string nodeName = NextName("node");
        BeginLine();
        Push("var ");
        Push(nodeName);
        Push(" = new ");
        Push(ComponentsNamespace);
        Push(".TextNode(");
        AppendExpression(text);
        if (patchFlags != 0)
        {
            Push(", renderPlan: new ");
            Push(ComponentsNamespace);
            Push(".RenderPlan((");
            Push(ComponentsNamespace);
            Push(".PatchFlags)");
            Push("(");
            Push(patchFlags.ToString(CultureInfo.InvariantCulture));
            Push(")");
            Push(")");
        }

        Push(");");
        EndLine();
        if (patchFlags > 0 && patchFlags != (int)PatchFlags.NeedsHydration)
        {
            AppendStatement($"frame.Track({nodeName});");
        }

        return CodeExpression.Literal(nodeName);
    }

    private CodeExpression EmitCommentNode(string text)
    {
        string nodeName = NextName("node");
        AppendStatement($"var {nodeName} = new {ComponentsNamespace}.CommentNode({text});");
        return CodeExpression.Literal(nodeName);
    }

    private CodeExpression EmitStaticNode(CodeExpression content)
    {
        string nodeName = NextName("node");
        BeginLine();
        Push("var ");
        Push(nodeName);
        Push(" = new ");
        Push(ComponentsNamespace);
        Push(".StaticNode(");
        Push(ComponentsNamespace);
        Push(".MarkupFormat.Html, ");
        AppendExpression(content);
        Push(");");
        EndLine();
        return CodeExpression.Literal(nodeName);
    }

    private void TrackIfRequired(VirtualNodeCall node, string nodeName)
    {
        int patchFlags = node.PatchFlag is null ? 0 : (int)node.PatchFlag.Value;
        bool shouldTrack = node.IsBlock
            || node.IsComponent
            || (patchFlags > 0 && patchFlags != (int)PatchFlags.NeedsHydration);
        if (shouldTrack)
        {
            AppendStatement($"frame.Track({nodeName});");
        }
    }

    private CodeExpression EmitRenderPlan(VirtualNodeCall node, CodeExpression dynamicBindingIndices)
    {
        int patchFlags = node.PatchFlag is null ? 0 : (int)node.PatchFlag.Value;
        if (!node.IsBlock && patchFlags == 0 && dynamicBindingIndices.Text == "null")
        {
            return CodeExpression.Literal("null");
        }

        var expression = new CodeExpression();
        expression.Append("new ");
        expression.Append(ComponentsNamespace);
        expression.Append(".RenderPlan((");
        expression.Append(ComponentsNamespace);
        expression.Append(".PatchFlags)");
        expression.Append("(");
        expression.Append(patchFlags.ToString(CultureInfo.InvariantCulture));
        expression.Append(")");
        expression.Append(" /* ");
        expression.Append(PatchFlagNames.Format((PatchFlags)patchFlags));
        expression.Append(" */, ");
        expression.Append(dynamicBindingIndices);
        if (node.IsBlock)
        {
            expression.Append(", frame.CloseBlock()");
        }

        expression.Append(")");
        return expression;
    }

    private ElementPropertyEmission EmitElementProperties(TemplateSyntaxNode? propertiesNode)
    {
        CodeExpression properties = EmitPropertyDictionary(
            propertiesNode,
            PropertyValuePurpose.Element);
        string propertiesName = MaterializeExpression("properties", properties);
        string bindingsName = NextName("bindings");
        string keyName = NextName("key");
        string mountReferenceName = NextName("mountReference");
        string propertyName = NextName("property");
        string bindingName = NextName("bindingName");
        string listenerName = NextName("listener");

        AppendStatement(
            $"var {bindingsName} = new global::System.Collections.Generic.List<{ComponentsNamespace}.ElementBinding>();");
        AppendStatement($"object? {keyName} = null;");
        AppendStatement($"{ComponentsNamespace}.MountReference? {mountReferenceName} = null;");
        AppendLine($"foreach (global::System.Collections.Generic.KeyValuePair<string, object?> {propertyName} in {propertiesName})");
        AppendLine("{");
        indentLevel++;
        AppendStatement($"string {bindingName} = {propertyName}.Key;");
        AppendLine($"if (global::System.String.Equals({bindingName}, \"key\", global::System.StringComparison.Ordinal))");
        AppendLine("{");
        indentLevel++;
        AppendStatement($"{keyName} = {propertyName}.Value;");
        indentLevel--;
        AppendLine("}");
        AppendLine($"else if (global::System.String.Equals({bindingName}, \"ref\", global::System.StringComparison.Ordinal))");
        AppendLine("{");
        indentLevel++;
        AppendStatement($"{mountReferenceName} = {propertyName}.Value as {ComponentsNamespace}.MountReference;");
        indentLevel--;
        AppendLine("}");
        AppendLine($"else if ({GeneratedEventNameTest(bindingName)})");
        AppendLine("{");
        indentLevel++;
        AppendLine($"if (!global::System.String.Equals({bindingName}, \"onUpdate:modelValue\", global::System.StringComparison.Ordinal)"
            + $" && {propertyName}.Value is global::System.Delegate {listenerName})");
        AppendLine("{");
        indentLevel++;
        AppendStatement(
            $"{bindingsName}.Add({ComponentsNamespace}.ElementBinding.Event({GeneratedEventNameValue(bindingName)}, {listenerName}));");
        indentLevel--;
        AppendLine("}");
        indentLevel--;
        AppendLine("}");
        AppendLine($"else if ({bindingName}.Length > 0 && {bindingName}[0] == '.')");
        AppendLine("{");
        indentLevel++;
        AppendStatement(
            $"{bindingsName}.Add({ComponentsNamespace}.ElementBinding.Property({bindingName}.Substring(1), {propertyName}.Value));");
        indentLevel--;
        AppendLine("}");
        AppendLine($"else if ({bindingName}.Length > 0 && {bindingName}[0] == '^')");
        AppendLine("{");
        indentLevel++;
        AppendStatement(
            $"{bindingsName}.Add({ComponentsNamespace}.ElementBinding.Attribute(new {ComponentsNamespace}.QualifiedName({bindingName}.Substring(1)), {propertyName}.Value));");
        indentLevel--;
        AppendLine("}");
        AppendLine($"else if ({GeneratedHostPropertyNameTest(bindingName)})");
        AppendLine("{");
        indentLevel++;
        AppendStatement(
            $"{bindingsName}.Add({ComponentsNamespace}.ElementBinding.Property({bindingName}, {propertyName}.Value));");
        indentLevel--;
        AppendLine("}");
        AppendLine("else");
        AppendLine("{");
        indentLevel++;
        AppendStatement(
            $"{bindingsName}.Add({ComponentsNamespace}.ElementBinding.Attribute(new {ComponentsNamespace}.QualifiedName({bindingName}), {propertyName}.Value));");
        indentLevel--;
        AppendLine("}");
        indentLevel--;
        AppendLine("}");

        return new ElementPropertyEmission(
            propertiesName,
            bindingsName,
            keyName,
            mountReferenceName);
    }

    private GenericPropertyEmission EmitGenericProperties(
        TemplateSyntaxNode? propertiesNode,
        PropertyValuePurpose purpose)
    {
        CodeExpression properties = EmitPropertyDictionary(propertiesNode, purpose);
        string propertiesName = MaterializeExpression("properties", properties);
        string keyName = NextName("key");
        string mountReferenceName = NextName("mountReference");
        string keyValueName = NextName("keyValue");
        string referenceValueName = NextName("referenceValue");

        AppendStatement(
            $"object? {keyName} = {propertiesName}.TryGetValue(\"key\", out object? {keyValueName}) ? {keyValueName} : null;");
        AppendStatement(
            $"{ComponentsNamespace}.MountReference? {mountReferenceName} = {propertiesName}.TryGetValue(\"ref\", out object? {referenceValueName})"
            + $" ? {referenceValueName} as {ComponentsNamespace}.MountReference : null;");

        return new GenericPropertyEmission(propertiesName, keyName, mountReferenceName);
    }

    private ComponentInvocationEmission EmitComponentInvocation(
        TemplateSyntaxNode? propertiesNode,
        object? childrenNode,
        ArrayExpression? directivesNode)
    {
        CodeExpression properties = EmitPropertyDictionary(
            propertiesNode,
            PropertyValuePurpose.Component);
        string propertiesName = MaterializeExpression("properties", properties);
        SlotEmission slots = EmitSlots(childrenNode);
        CodeExpression directives = EmitDirectiveInvocations(directivesNode);
        string argumentsName = NextName("arguments");
        string listenersName = NextName("listeners");
        string keyName = NextName("key");
        string mountReferenceName = NextName("mountReference");
        string propertyName = NextName("property");
        string listenerName = NextName("listener");

        AppendStatement(
            $"var {argumentsName} = new global::System.Collections.Generic.Dictionary<string, object?>(global::System.StringComparer.Ordinal);");
        AppendStatement(
            $"var {listenersName} = new global::System.Collections.Generic.Dictionary<string, {ComponentsNamespace}.ComponentEventListener>(global::System.StringComparer.Ordinal);");
        AppendStatement($"object? {keyName} = null;");
        AppendStatement($"{ComponentsNamespace}.MountReference? {mountReferenceName} = null;");
        AppendLine($"foreach (global::System.Collections.Generic.KeyValuePair<string, object?> {propertyName} in {propertiesName})");
        AppendLine("{");
        indentLevel++;
        AppendLine($"if (global::System.String.Equals({propertyName}.Key, \"key\", global::System.StringComparison.Ordinal))");
        AppendLine("{");
        indentLevel++;
        AppendStatement($"{keyName} = {propertyName}.Value;");
        indentLevel--;
        AppendLine("}");
        AppendLine($"else if (global::System.String.Equals({propertyName}.Key, \"ref\", global::System.StringComparison.Ordinal))");
        AppendLine("{");
        indentLevel++;
        AppendStatement($"{mountReferenceName} = {propertyName}.Value as {ComponentsNamespace}.MountReference;");
        indentLevel--;
        AppendLine("}");
        AppendLine($"else if ({GeneratedEventNameTest(propertyName + ".Key")}"
            + $" && !{propertyName}.Key.StartsWith(\"onVnode\", global::System.StringComparison.Ordinal)"
            + $" && {propertyName}.Value is {ComponentsNamespace}.ComponentEventListener {listenerName})");
        AppendLine("{");
        indentLevel++;
        AppendStatement($"{listenersName}[{GeneratedEventNameValue(propertyName + ".Key")}] = {listenerName};");
        indentLevel--;
        AppendLine("}");
        AppendLine("else");
        AppendLine("{");
        indentLevel++;
        AppendStatement($"{argumentsName}[{propertyName}.Key] = {propertyName}.Value;");
        indentLevel--;
        AppendLine("}");
        indentLevel--;
        AppendLine("}");

        string invocationName = NextName("invocation");
        BeginLine();
        Push("var ");
        Push(invocationName);
        Push(" = new ");
        Push(ComponentsNamespace);
        Push(".ComponentInvocation(arguments: ");
        Push(argumentsName);
        Push(", slots: ");
        Push(slots.SlotsName);
        Push(", listeners: ");
        Push(listenersName);
        Push(", directives: ");
        AppendExpression(directives);
        Push(", slotStability: (");
        Push(ComponentsNamespace);
        Push(".SlotStability)");
        Push(slots.Stability.ToString(CultureInfo.InvariantCulture));
        Push(");");
        EndLine();

        return new ComponentInvocationEmission(
            invocationName,
            keyName,
            mountReferenceName);
    }

    private CodeExpression EmitPropertyDictionary(
        TemplateSyntaxNode? node,
        PropertyValuePurpose purpose)
    {
        switch (node)
        {
            case null:
                return EmptyPropertyDictionary();
            case ObjectExpression objectExpression:
                return EmitObjectDictionary(objectExpression, purpose);
            case CacheExpression cache:
                return EmitCachedPropertyDictionary(cache, purpose);
            case CallExpression call when call.Callee is RuntimeHelper helper:
                if (helper == HelperNames.MergeProps)
                {
                    var expression = new CodeExpression();
                    expression.Append(CoreNamespace);
                    expression.Append(".PropertyNormalization.Merge(");
                    for (int index = 0; index < call.Arguments.Count; index++)
                    {
                        if (index > 0)
                        {
                            expression.Append(", ");
                        }

                        expression.Append(EmitPropertyDictionary(
                            call.Arguments[index] as TemplateSyntaxNode,
                            purpose));
                    }

                    expression.Append(")");
                    return expression;
                }

                if (helper == HelperNames.NormalizeProps)
                {
                    var expression = new CodeExpression();
                    expression.Append(CoreNamespace);
                    expression.Append(".PropertyNormalization.Normalize(");
                    expression.Append(EmitPropertyDictionary(
                        call.Arguments.Count == 0 ? null : call.Arguments[0] as TemplateSyntaxNode,
                        purpose));
                    expression.Append(")");
                    return expression;
                }

                if (helper == HelperNames.GuardReactiveProps)
                {
                    return EmitPropertyDictionary(
                        call.Arguments.Count == 0 ? null : call.Arguments[0] as TemplateSyntaxNode,
                        purpose);
                }

                if (helper == HelperNames.ToHandlers)
                {
                    return EmitHandlerDictionary(call, purpose);
                }

                return NormalizePropertyExpression(EmitCallExpression(call));
            default:
                return NormalizePropertyExpression(EmitExpression(node));
        }
    }

    private CodeExpression EmitObjectDictionary(
        ObjectExpression objectExpression,
        PropertyValuePurpose purpose)
    {
        var expression = new CodeExpression();
        if (objectExpression.IsDirectiveModifiers)
        {
            expression.Append("new global::System.Collections.Generic.Dictionary<string, bool>(global::System.StringComparer.Ordinal) { ");
        }
        else
        {
            expression.Append("new global::System.Collections.Generic.Dictionary<string, object?>(global::System.StringComparer.Ordinal) { ");
        }

        for (int index = 0; index < objectExpression.Properties.Count; index++)
        {
            ObjectProperty property = objectExpression.Properties[index];
            if (index > 0)
            {
                expression.Append(", ");
            }

            expression.Append("[");
            expression.Append(EmitPropertyKey(property.Key));
            expression.Append("] = ");
            expression.Append(EmitPropertyValue(property, purpose));
        }

        expression.Append(" }");
        return expression;
    }

    private CodeExpression EmitPropertyValue(ObjectProperty property, PropertyValuePurpose purpose)
    {
        bool isHandler = IsHandlerKey(property.Key);
        bool isReference = IsStaticPropertyName(property.Key, "ref");
        if (isHandler)
        {
            if (property.Value is CacheExpression cache)
            {
                return EmitCachedHandler(cache, purpose);
            }

            CodeExpression handler = EmitExpression(property.Value);
            var expression = new CodeExpression();
            expression.Append(GeneratedNamespace);
            expression.Append(purpose == PropertyValuePurpose.Component
                ? ".RenderGlue.ComponentListener("
                : ".RenderGlue.Handler(");
            expression.Append(handler);
            expression.Append(")");
            return expression;
        }

        if (isReference)
        {
            var expression = new CodeExpression();
            expression.Append("(");
            expression.Append(ComponentsNamespace);
            expression.Append(".MountReference)(");
            if (property.Value is SimpleExpressionNode { IsStatic: true } staticReference)
            {
                expression.Append("referenceValue => component.");
                expression.Append(MapIdentifier(staticReference.Content));
                expression.Append(" = referenceValue");
            }
            else
            {
                expression.Append(EmitExpression(property.Value));
            }

            expression.Append(")");
            return expression;
        }

        return EmitExpression(property.Value);
    }

    private CodeExpression EmitCachedHandler(
        CacheExpression cache,
        PropertyValuePurpose purpose)
    {
        CodeExpression handler = EmitExpression(cache.Value);
        string handlerName = NextName("handler");
        BeginLine();
        Push("var ");
        Push(handlerName);
        Push(" = ");
        Push(GeneratedNamespace);
        Push(purpose == PropertyValuePurpose.Component
            ? ".RenderGlue.ComponentListener("
            : ".RenderGlue.Handler(");
        AppendExpression(handler);
        Push(");");
        EndLine();

        string cachedName = NextName("cachedHandler");
        AppendStatement(
            $"var {cachedName} = frame.CacheHandler({cache.Index.ToString(CultureInfo.InvariantCulture)}, {handlerName});");
        return CodeExpression.Literal(cachedName);
    }

    private CodeExpression EmitCachedPropertyDictionary(
        CacheExpression cache,
        PropertyValuePurpose purpose)
    {
        if (cache.Value is not TemplateSyntaxNode valueNode)
        {
            return EmptyPropertyDictionary();
        }

        CodeExpression value = EmitPropertyDictionary(valueNode, purpose);
        string factoryName = NextName("createCachedProperties");
        AppendLine($"global::System.Collections.Generic.IReadOnlyDictionary<string, object?> {factoryName}()");
        AppendLine("{");
        indentLevel++;
        BeginLine();
        Push("return ");
        AppendExpression(value);
        Push(";");
        EndLine();
        indentLevel--;
        AppendLine("}");

        var expression = new CodeExpression();
        expression.Append("frame.GetOrAddCache<global::System.Collections.Generic.IReadOnlyDictionary<string, object?>>(");
        expression.Append(cache.Index.ToString(CultureInfo.InvariantCulture));
        expression.Append(", ");
        expression.Append(factoryName);
        expression.Append(")");
        return expression;
    }

    private CodeExpression EmitHandlerDictionary(
        CallExpression call,
        PropertyValuePurpose purpose)
    {
        CodeExpression source = call.Arguments.Count == 0
            ? CodeExpression.Literal("null")
            : EmitExpression(call.Arguments[0]);
        string sourceName = MaterializeExpression("handlerSource", source);
        string handlersName = NextName("handlers");
        string handlerName = NextName("handler");
        string keyName = NextName("handlerKey");
        AppendStatement(
            $"var {handlersName} = new global::System.Collections.Generic.Dictionary<string, object?>(global::System.StringComparer.Ordinal);");
        AppendLine($"foreach (var {handlerName} in {sourceName})");
        AppendLine("{");
        indentLevel++;
        AppendStatement(
            $"string {keyName} = \"on\" + {ComponentsNamespace}.NameNormalization.Pascalize({handlerName}.Key);");
        BeginLine();
        Push(handlersName);
        Push("[");
        Push(keyName);
        Push("] = ");
        Push(GeneratedNamespace);
        Push(purpose == PropertyValuePurpose.Component
            ? ".RenderGlue.ComponentListener("
            : ".RenderGlue.Handler(");
        Push(handlerName);
        Push(".Value);");
        EndLine();
        indentLevel--;
        AppendLine("}");
        return CodeExpression.Literal(handlersName);
    }

    private static CodeExpression EmptyPropertyDictionary() =>
        CodeExpression.Literal(
            "new global::System.Collections.Generic.Dictionary<string, object?>(global::System.StringComparer.Ordinal)");

    private CodeExpression NormalizePropertyExpression(CodeExpression expression)
    {
        var normalized = new CodeExpression();
        normalized.Append(CoreNamespace);
        normalized.Append(".PropertyNormalization.Normalize(");
        normalized.Append(expression);
        normalized.Append(")");
        return normalized;
    }

    private CodeExpression EmitPropertyKey(ExpressionNode key)
    {
        if (key is SimpleExpressionNode { IsStatic: true } staticKey)
        {
            return CodeExpression.Literal(StringLiteral(staticKey.Content));
        }

        return EmitExpression(key);
    }

    private static bool IsStaticPropertyName(ExpressionNode key, string name) =>
        key is SimpleExpressionNode { IsStatic: true } simple
        && string.Equals(simple.Content, name, StringComparison.Ordinal);

    private static bool IsHandlerKey(ExpressionNode key) => key switch
    {
        SimpleExpressionNode { IsHandlerKey: true } => true,
        CompoundExpressionNode { IsHandlerKey: true } => true,
        SimpleExpressionNode { IsStatic: true } simple => IsGeneratedEventName(simple.Content),
        _ => false,
    };

    private SlotEmission EmitSlots(object? node)
    {
        string slotsName = NextName("slots");
        AppendStatement(
            $"var {slotsName} = new global::System.Collections.Generic.Dictionary<string, {ComponentsNamespace}.ComponentSlot>(global::System.StringComparer.Ordinal);");
        int stability = (int)SlotStability.Stable;

        switch (node)
        {
            case null:
                break;
            case ObjectExpression objectExpression:
                stability = AppendStaticSlots(objectExpression, slotsName, stability);
                break;
            case CallExpression call when call.Callee is RuntimeHelper helper
                && helper == HelperNames.CreateSlots:
                if (call.Arguments.Count > 0 && call.Arguments[0] is ObjectExpression baseSlots)
                {
                    stability = AppendStaticSlots(baseSlots, slotsName, stability);
                }

                if (call.Arguments.Count > 1 && call.Arguments[1] is ArrayExpression dynamicSlots)
                {
                    for (int index = 0; index < dynamicSlots.Elements.Count; index++)
                    {
                        AppendDynamicSlot(dynamicSlots.Elements[index], slotsName);
                    }
                }

                stability = (int)SlotStability.Dynamic;
                break;
            default:
                // Component children are expected to be a compiled slot set. Preserve a useful default
                // slot if a future transform supplies an ordinary subtree directly.
                CodeExpression value = EmitVirtualValue(node);
                string slotName = NextName("slot");
                AppendStatement(
                    $"{ComponentsNamespace}.ComponentSlot {slotName} = slotArguments => {value.Text};");
                AppendStatement($"{slotsName}[\"default\"] = {slotName};");
                stability = (int)SlotStability.Dynamic;
                break;
        }

        return new SlotEmission(slotsName, stability);
    }

    private int AppendStaticSlots(
        ObjectExpression objectExpression,
        string slotsName,
        int stability)
    {
        for (int index = 0; index < objectExpression.Properties.Count; index++)
        {
            ObjectProperty property = objectExpression.Properties[index];
            if (IsStaticPropertyName(property.Key, "_"))
            {
                stability = ParseRawInteger(property.Value);
                continue;
            }

            CodeExpression name = EmitPropertyKey(property.Key);
            if (property.Value is not FunctionExpression function)
            {
                continue;
            }

            CodeExpression slot = EmitSlotFunction(function);
            BeginLine();
            Push(slotsName);
            Push("[");
            AppendExpression(name);
            Push("] = ");
            AppendExpression(slot);
            Push(";");
            EndLine();
        }

        return stability;
    }

    private CodeExpression EmitSlotFunction(FunctionExpression function)
    {
        string slotName = NextName("slot");
        string parameterName = "slotArguments";
        if (function.Parameters.Count > 0)
        {
            parameterName = ParameterName(function.Parameters[0], "slotArguments");
        }

        AppendLine(
            $"{ComponentsNamespace}.ComponentSlot {slotName} = ({parameterName}) =>");
        AppendLine("{");
        indentLevel++;

        CodeExpression value;
        if (function.Returns is SyntaxList<TemplateChildNode> children)
        {
            value = EmitSlotRoot(children);
        }
        else if (function.Returns is not null)
        {
            value = EmitVirtualValue(function.Returns);
        }
        else
        {
            value = CodeExpression.Literal("null");
        }

        BeginLine();
        Push("return ");
        AppendExpression(value);
        Push(";");
        EndLine();
        indentLevel--;
        AppendLine("};");
        return CodeExpression.Literal(slotName);
    }

    private CodeExpression EmitSlotRoot(IReadOnlyList<TemplateChildNode> children)
    {
        if (children.Count == 0)
        {
            return CodeExpression.Literal("null");
        }

        if (children.Count == 1)
        {
            return EmitVirtualValue(children[0]);
        }

        CodeExpression childCollection = EmitChildren(children);
        string fragmentName = NextName("slotFragment");
        BeginLine();
        Push("var ");
        Push(fragmentName);
        Push(" = new ");
        Push(ComponentsNamespace);
        Push(".FragmentNode(");
        AppendExpression(childCollection);
        Push(");");
        EndLine();
        return CodeExpression.Literal(fragmentName);
    }

    private void AppendDynamicSlot(object? node, string slotsName)
    {
        switch (node)
        {
            case null:
            case string raw when raw is "undefined" or "void 0":
                return;
            case ObjectExpression descriptor:
                AppendDynamicSlotDescriptor(descriptor, slotsName);
                return;
            case ConditionalExpression conditional:
                BeginLine();
                Push("if (");
                AppendExpression(EmitExpression(conditional.Test));
                Push(")");
                EndLine();
                AppendLine("{");
                indentLevel++;
                AppendDynamicSlot(conditional.Consequent, slotsName);
                indentLevel--;
                AppendLine("}");
                AppendLine("else");
                AppendLine("{");
                indentLevel++;
                AppendDynamicSlot(conditional.Alternate, slotsName);
                indentLevel--;
                AppendLine("}");
                return;
            case CallExpression call when call.Callee is RuntimeHelper helper
                && helper == HelperNames.RenderList:
                AppendDynamicSlotList(call, slotsName);
                return;
            case ArrayExpression descriptors:
                for (int index = 0; index < descriptors.Elements.Count; index++)
                {
                    AppendDynamicSlot(descriptors.Elements[index], slotsName);
                }

                return;
            default:
                return;
        }
    }

    private void AppendDynamicSlotDescriptor(
        ObjectExpression descriptor,
        string slotsName)
    {
        ObjectProperty? nameProperty = FindStaticProperty(descriptor, "name");
        ObjectProperty? functionProperty = FindStaticProperty(descriptor, "fn");
        if (nameProperty is null || functionProperty?.Value is not FunctionExpression function)
        {
            return;
        }

        CodeExpression name = EmitExpression(nameProperty.Value);
        CodeExpression slot = EmitSlotFunction(function);
        BeginLine();
        Push(slotsName);
        Push("[");
        AppendExpression(name);
        Push("] = ");
        AppendExpression(slot);
        Push(";");
        EndLine();
    }

    private void AppendDynamicSlotList(CallExpression call, string slotsName)
    {
        if (call.Arguments.Count < 2 || call.Arguments[1] is not FunctionExpression function)
        {
            return;
        }

        CodeExpression source = EmitExpression(call.Arguments[0]);
        string sourceName = MaterializeExpression("slotSource", source);
        string itemName = function.Parameters.Count > 0
            ? ParameterName(function.Parameters[0], "slotItem")
            : NextName("slotItem");
        AppendLine($"foreach (var {itemName} in {sourceName})");
        AppendLine("{");
        indentLevel++;
        AppendDynamicSlot(function.Returns, slotsName);
        indentLevel--;
        AppendLine("}");
    }

    private static ObjectProperty? FindStaticProperty(ObjectExpression expression, string name)
    {
        for (int index = 0; index < expression.Properties.Count; index++)
        {
            ObjectProperty property = expression.Properties[index];
            if (IsStaticPropertyName(property.Key, name))
            {
                return property;
            }
        }

        return null;
    }

    private CodeExpression EmitSlotOutlet(CallExpression call)
    {
        CodeExpression slotName = call.Arguments.Count > 1
            ? EmitExpression(call.Arguments[1])
            : CodeExpression.Literal(StringLiteral("default"));
        CodeExpression arguments = call.Arguments.Count > 2
            && call.Arguments[2] is TemplateSyntaxNode argumentNode
            ? EmitPropertyDictionary(argumentNode, PropertyValuePurpose.Generic)
            : EmptyPropertyDictionary();
        string slotNameValue = MaterializeExpression("slotName", slotName);
        string argumentsName = MaterializeExpression("slotArguments", arguments);
        string slotNameLocal = NextName("slot");
        string resultName = NextName("slotNode");
        AppendStatement($"{ComponentsNamespace}.VirtualNode? {resultName};");
        AppendLine(
            $"if (component.Context!.Bindings.Slots.TryGetValue({slotNameValue}, out {ComponentsNamespace}.ComponentSlot? {slotNameLocal}))");
        AppendLine("{");
        indentLevel++;
        AppendStatement($"{resultName} = {slotNameLocal}({argumentsName});");
        indentLevel--;
        AppendLine("}");
        AppendLine("else");
        AppendLine("{");
        indentLevel++;

        CodeExpression fallback = CodeExpression.Literal("null");
        if (call.Arguments.Count > 3 && call.Arguments[3] is FunctionExpression fallbackFunction)
        {
            if (fallbackFunction.Returns is SyntaxList<TemplateChildNode> fallbackChildren)
            {
                fallback = EmitSlotRoot(fallbackChildren);
            }
            else if (fallbackFunction.Returns is not null)
            {
                fallback = EmitVirtualValue(fallbackFunction.Returns);
            }
        }

        BeginLine();
        Push(resultName);
        Push(" = ");
        AppendExpression(fallback);
        Push(";");
        EndLine();
        indentLevel--;
        AppendLine("}");
        AppendLine($"if ({resultName} is not null)");
        AppendLine("{");
        indentLevel++;
        AppendStatement($"frame.Track({resultName});");
        indentLevel--;
        AppendLine("}");
        return CodeExpression.Literal(resultName);
    }

    private CodeExpression EmitChildren(object? node)
    {
        switch (node)
        {
            case null:
                return CodeExpression.Literal("null");
            case SyntaxList<TemplateChildNode> children:
                return EmitChildren(children);
            case CallExpression call when call.Callee is RuntimeHelper helper
                && helper == HelperNames.RenderList:
                return EmitRenderList(call);
            case CacheExpression cache:
                return EmitCachedChildren(cache);
            default:
                string childrenName = CreateChildList();
                AppendChild(childrenName, EmitVirtualValue(node));
                return CodeExpression.Literal(childrenName);
        }
    }

    private CodeExpression EmitChildren(IReadOnlyList<TemplateChildNode> children)
    {
        string childrenName = CreateChildList();
        for (int index = 0; index < children.Count; index++)
        {
            AppendChild(childrenName, EmitVirtualValue(children[index]));
        }

        return CodeExpression.Literal(childrenName);
    }

    private string CreateChildList()
    {
        string childrenName = NextName("children");
        AppendStatement(
            $"var {childrenName} = new global::System.Collections.Generic.List<{ComponentsNamespace}.VirtualNode>();");
        return childrenName;
    }

    private void AppendChild(string childrenName, CodeExpression child)
    {
        string childName = NextName("child");
        BeginLine();
        Push("if (");
        AppendExpression(child);
        Push(" is ");
        Push(ComponentsNamespace);
        Push(".VirtualNode ");
        Push(childName);
        Push(")");
        EndLine();
        AppendLine("{");
        indentLevel++;
        AppendStatement($"{childrenName}.Add({childName});");
        indentLevel--;
        AppendLine("}");
    }

    private CodeExpression EmitRenderList(CallExpression call)
    {
        if (call.Arguments.Count < 2 || call.Arguments[1] is not FunctionExpression function)
        {
            return CodeExpression.Literal(
                $"new global::System.Collections.Generic.List<{ComponentsNamespace}.VirtualNode>()");
        }

        CodeExpression source = EmitExpression(call.Arguments[0]);
        string sourceName = MaterializeExpression("listSource", source);
        string resultName = NextName("listNodes");
        string indexName = NextName("listIndex");
        string itemName = NextName("listItem");
        AppendStatement(
            $"var {resultName} = new global::System.Collections.Generic.List<{ComponentsNamespace}.VirtualNode>();");
        AppendStatement($"int {indexName} = 0;");

        bool hasMemoBody = function.Body is not null && call.Arguments.Count >= 4;
        string? previousMemoEntriesName = null;
        string? currentMemoEntriesName = null;
        int memoSlot = 0;
        if (hasMemoBody)
        {
            memoSlot = ParseRawInteger(call.Arguments[3]);
            previousMemoEntriesName = NextName("previousMemoEntries");
            currentMemoEntriesName = NextName("currentMemoEntries");
            string entryType = MemoEntryType();
            AppendStatement(
                $"global::System.Collections.Generic.IReadOnlyList<{entryType}>? {previousMemoEntriesName} = frame.Cache[{memoSlot.ToString(CultureInfo.InvariantCulture)}] as global::System.Collections.Generic.IReadOnlyList<{entryType}>;");
            AppendStatement(
                $"var {currentMemoEntriesName} = new global::System.Collections.Generic.List<{entryType}>();");
        }

        AppendLine($"foreach (var {itemName} in {sourceName})");
        AppendLine("{");
        indentLevel++;
        AppendForAliases(function.Parameters, itemName, indexName, hasMemoBody);
        if (hasMemoBody
            && TryReadMemoLoop(function.Body!, out TemplateSyntaxNode dependenciesNode, out TemplateSyntaxNode? keyNode, out TemplateSyntaxNode childNode))
        {
            AppendMemoizedListItem(
                dependenciesNode,
                keyNode,
                childNode,
                previousMemoEntriesName!,
                currentMemoEntriesName!,
                resultName,
                indexName);
        }
        else
        {
            CodeExpression item = function.Returns is null
                ? CodeExpression.Literal("null")
                : EmitVirtualValue(function.Returns);
            BeginLine();
            Push("if (");
            AppendExpression(item);
            Push(" is ");
            Push(ComponentsNamespace);
            string itemNodeName = NextName("itemNode");
            Push(".VirtualNode ");
            Push(itemNodeName);
            Push(")");
            EndLine();
            AppendLine("{");
            indentLevel++;
            AppendStatement($"{resultName}.Add({itemNodeName});");
            indentLevel--;
            AppendLine("}");
        }

        AppendStatement($"{indexName}++;");
        indentLevel--;
        AppendLine("}");
        if (hasMemoBody)
        {
            AppendStatement(
                $"frame.SetCache({memoSlot.ToString(CultureInfo.InvariantCulture)}, {currentMemoEntriesName});");
        }

        return CodeExpression.Literal(resultName);
    }

    private void AppendForAliases(
        IReadOnlyList<object> parameters,
        string itemName,
        string indexName,
        bool hasMemoBody)
    {
        int parameterCount = hasMemoBody ? parameters.Count - 1 : parameters.Count;
        if (parameterCount <= 0)
        {
            return;
        }

        string valueName = ParameterName(parameters[0], "item");
        bool hasKey = parameterCount >= 2 && !IsSyntheticAlias(parameters[1]);
        bool hasIndex = parameterCount >= 3 && !IsSyntheticAlias(parameters[2]);
        if (hasIndex)
        {
            AppendStatement($"var {valueName} = {itemName}.Value;");
            string keyName = ParameterName(parameters[1], "key");
            string declaredIndexName = ParameterName(parameters[2], "index");
            AppendStatement($"var {keyName} = {itemName}.Key;");
            AppendStatement($"var {declaredIndexName} = {indexName};");
            return;
        }

        AppendStatement($"var {valueName} = {itemName};");
        if (hasKey)
        {
            string declaredIndexName = ParameterName(parameters[1], "index");
            AppendStatement($"var {declaredIndexName} = {indexName};");
        }
    }

    private void AppendMemoizedListItem(
        TemplateSyntaxNode dependenciesNode,
        TemplateSyntaxNode? keyNode,
        TemplateSyntaxNode childNode,
        string previousEntriesName,
        string currentEntriesName,
        string resultName,
        string indexName)
    {
        string dependenciesName = NextName("memoDependencies");
        string keyName = NextName("memoKey");
        string nodeName = NextName("memoizedItem");
        BeginLine();
        Push("global::System.Collections.Generic.IReadOnlyList<object?> ");
        Push(dependenciesName);
        Push(" = ");
        AppendExpression(EmitExpression(dependenciesNode));
        Push(";");
        EndLine();
        BeginLine();
        Push("object? ");
        Push(keyName);
        Push(" = ");
        AppendExpression(keyNode is null ? CodeExpression.Literal("null") : EmitExpression(keyNode));
        Push(";");
        EndLine();
        AppendStatement($"{ComponentsNamespace}.VirtualNode? {nodeName};");
        AppendLine(
            $"if ({previousEntriesName} is not null"
            + $" && {indexName} < {previousEntriesName}.Count"
            + $" && global::System.Object.Equals({previousEntriesName}[{indexName}].Key, {keyName})"
            + $" && global::System.Linq.Enumerable.SequenceEqual({previousEntriesName}[{indexName}].Dependencies, {dependenciesName}))");
        AppendLine("{");
        indentLevel++;
        AppendStatement($"{nodeName} = {previousEntriesName}[{indexName}].Node;");
        AppendLine($"if ({nodeName} is not null)");
        AppendLine("{");
        indentLevel++;
        AppendStatement($"frame.Track({nodeName});");
        indentLevel--;
        AppendLine("}");
        indentLevel--;
        AppendLine("}");
        AppendLine("else");
        AppendLine("{");
        indentLevel++;
        CodeExpression child = EmitVirtualValue(childNode);
        BeginLine();
        Push(nodeName);
        Push(" = ");
        AppendExpression(child);
        Push(";");
        EndLine();
        indentLevel--;
        AppendLine("}");
        AppendLine($"if ({nodeName} is not null)");
        AppendLine("{");
        indentLevel++;
        AppendStatement($"{resultName}.Add({nodeName});");
        indentLevel--;
        AppendLine("}");
        AppendStatement($"{currentEntriesName}.Add(({keyName}, {dependenciesName}, {nodeName}));");
    }

    private static bool TryReadMemoLoop(
        BlockStatement body,
        out TemplateSyntaxNode dependencies,
        out TemplateSyntaxNode? key,
        out TemplateSyntaxNode child)
    {
        dependencies = null!;
        key = null;
        child = null!;
        if (body.Body.Count < 3
            || body.Body[0] is not CompoundExpressionNode dependenciesStatement
            || dependenciesStatement.Parts.Count < 2
            || dependenciesStatement.Parts[1] is not TemplateSyntaxNode dependencyNode
            || body.Body[1] is not CompoundExpressionNode condition
            || body.Body[2] is not CompoundExpressionNode childStatement
            || childStatement.Parts.Count < 2
            || childStatement.Parts[1] is not TemplateSyntaxNode childNode)
        {
            return false;
        }

        dependencies = dependencyNode;
        child = childNode;
        if (condition.Parts.Count > 1 && condition.Parts[1] is TemplateSyntaxNode keyNode)
        {
            key = keyNode;
        }

        return true;
    }

    private static bool IsSyntheticAlias(object parameter)
    {
        string name = parameter switch
        {
            SimpleExpressionNode simple => simple.Content,
            string raw => raw,
            _ => string.Empty,
        };
        if (name.Length == 0)
        {
            return true;
        }

        for (int index = 0; index < name.Length; index++)
        {
            if (name[index] != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static string MemoEntryType() =>
        $"(object? Key, global::System.Collections.Generic.IReadOnlyList<object?> Dependencies, {ComponentsNamespace}.VirtualNode? Node)";

    private CodeExpression EmitCachedChildren(CacheExpression cache)
    {
        CodeExpression cached = EmitCachedVirtualValue(cache);
        string childrenName = CreateChildList();
        AppendChild(childrenName, cached);
        return CodeExpression.Literal(childrenName);
    }

    private CodeExpression EmitDirectiveInvocations(ArrayExpression? directives)
    {
        if (directives is null || directives.Elements.Count == 0)
        {
            return CodeExpression.Literal("null");
        }

        string directivesName = NextName("directives");
        AppendStatement(
            $"var {directivesName} = new global::System.Collections.Generic.List<{ComponentsNamespace}.DirectiveInvocation>();");
        for (int index = 0; index < directives.Elements.Count; index++)
        {
            if (directives.Elements[index] is not ArrayExpression invocation
                || invocation.Elements.Count == 0)
            {
                continue;
            }

            string token = DirectiveTypeToken(invocation.Elements[0]);
            CodeExpression value = invocation.Elements.Count > 1
                ? EmitExpression(invocation.Elements[1])
                : CodeExpression.Literal("null");

            BeginLine();
            Push(directivesName);
            Push(".Add(new ");
            Push(ComponentsNamespace);
            Push(".DirectiveInvocation(typeof(");
            Push(token);
            Push("), ");
            AppendExpression(value);
            Push("));");
            EndLine();
        }

        return CodeExpression.Literal(directivesName);
    }

    private string DirectiveTypeToken(object token)
    {
        string raw = token switch
        {
            string value => value,
            RuntimeHelper helper => "_" + helper.Name,
            SimpleExpressionNode simple => simple.Content,
            _ => string.Empty,
        };

        if (raw == "_vShow")
        {
            return BrowserNamespace + ".VShow";
        }

        if (raw == "_vModelText")
        {
            return BrowserNamespace + ".VModelText";
        }

        if (raw == "_vModelCheckbox")
        {
            return BrowserNamespace + ".VModelCheckbox";
        }

        if (raw == "_vModelRadio")
        {
            return BrowserNamespace + ".VModelRadio";
        }

        if (raw == "_vModelSelect")
        {
            return BrowserNamespace + ".VModelSelect";
        }

        if (raw == "_vModelDynamic")
        {
            return BrowserNamespace + ".VModelDynamic";
        }

        string? directiveName = ResolveAssetName(raw, "directive", result.Directives);
        return directiveName is null
            ? ComponentsNamespace + ".DirectiveInvocation"
            : PascalizeDirectiveTypeName(directiveName);
    }

    private static string PascalizeDirectiveTypeName(string name)
    {
        var output = new StringBuilder(name.Length);
        bool capitalize = true;
        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];
            if (!char.IsLetterOrDigit(character) && character != '_')
            {
                capitalize = true;
                continue;
            }

            output.Append(capitalize ? char.ToUpperInvariant(character) : character);
            capitalize = false;
        }

        return output.Length == 0 ? "Directive" : output.ToString();
    }

    private CodeExpression EmitCachedVirtualValue(CacheExpression cache)
    {
        string factoryName = NextName("createCachedNode");
        AppendLine($"{ComponentsNamespace}.VirtualNode? {factoryName}()");
        AppendLine("{");
        indentLevel++;
        if (cache.NeedPauseTracking)
        {
            AppendStatement("frame.SetBlockTracking(-1);");
            AppendLine("try");
            AppendLine("{");
            indentLevel++;
        }

        CodeExpression value = EmitVirtualValue(cache.Value);
        BeginLine();
        Push("return ");
        AppendExpression(value);
        Push(";");
        EndLine();

        if (cache.NeedPauseTracking)
        {
            indentLevel--;
            AppendLine("}");
            AppendLine("finally");
            AppendLine("{");
            indentLevel++;
            AppendStatement("frame.SetBlockTracking(1);");
            indentLevel--;
            AppendLine("}");
        }

        indentLevel--;
        AppendLine("}");
        string valueName = NextName("cachedNode");
        AppendStatement(
            $"{ComponentsNamespace}.VirtualNode? {valueName} = frame.GetOrAddCache<{ComponentsNamespace}.VirtualNode?>({cache.Index.ToString(CultureInfo.InvariantCulture)}, {factoryName});");
        return CodeExpression.Literal(valueName);
    }

    private CodeExpression EmitMemoizedVirtualValue(CallExpression call)
    {
        if (call.Arguments.Count < 4)
        {
            throw new InvalidOperationException("A memo call requires dependencies, renderer, cache, and slot.");
        }

        CodeExpression dependencies = EmitExpression(call.Arguments[0]);
        if (call.Arguments[1] is not FunctionExpression renderer)
        {
            throw new InvalidOperationException("A memo call requires a render closure.");
        }

        int slot = ParseRawInteger(call.Arguments[3]);
        string factoryName = NextName("renderMemoNode");
        AppendLine($"{ComponentsNamespace}.VirtualNode? {factoryName}()");
        AppendLine("{");
        indentLevel++;
        CodeExpression value = renderer.Returns is null
            ? CodeExpression.Literal("null")
            : EmitVirtualValue(renderer.Returns);
        BeginLine();
        Push("return ");
        AppendExpression(value);
        Push(";");
        EndLine();
        indentLevel--;
        AppendLine("}");

        string resultName = NextName("memoizedNode");
        BeginLine();
        Push(ComponentsNamespace);
        Push(".VirtualNode? ");
        Push(resultName);
        Push(" = frame.Memo(");
        Push(slot.ToString(CultureInfo.InvariantCulture));
        Push(", ");
        AppendExpression(dependencies);
        Push(", ");
        Push(factoryName);
        Push(");");
        EndLine();
        return CodeExpression.Literal(resultName);
    }

    private CodeExpression EmitExpression(object? node)
    {
        switch (node)
        {
            case null:
                return CodeExpression.Literal("null");
            case string raw:
                return CodeExpression.Literal(MapRawLiteral(raw));
            case RuntimeHelper helper:
                return EmitRuntimeHelperExpression(helper);
            case TextNode text:
                return CodeExpression.Literal(StringLiteral(text.Content));
            case SimpleExpressionNode simple:
                return EmitSimpleExpression(simple);
            case InterpolationNode interpolation:
                return EmitDisplayString(interpolation.Content);
            case CompoundExpressionNode compound:
                return EmitCompoundExpression(compound);
            case CallExpression call:
                return EmitCallExpression(call);
            case ObjectExpression objectExpression:
                return EmitObjectDictionary(objectExpression, PropertyValuePurpose.Generic);
            case ArrayExpression array:
                return EmitArrayExpression(array);
            case FunctionExpression function:
                return EmitFunctionExpression(function);
            case CacheExpression cache:
                return EmitCachedVirtualValue(cache);
            case VirtualNodeCall virtualNode:
                return EmitVirtualNode(virtualNode);
            case ConditionalExpression conditional:
                return EmitConditionalVirtualValue(conditional);
            case SyntaxList<TemplateChildNode> children:
                return EmitChildren(children);
            default:
                throw new InvalidOperationException(
                    $"Unsupported expression node '{node.GetType().Name}'.");
        }
    }

    private CodeExpression EmitCallExpression(CallExpression call)
    {
        if (call.Callee is not RuntimeHelper helper)
        {
            var rawCall = new CodeExpression();
            rawCall.Append(MapRawLiteral((string)call.Callee));
            rawCall.Append("(");
            AppendCallArguments(rawCall, call.Arguments);
            rawCall.Append(")");
            return rawCall;
        }

        if (helper == HelperNames.NormalizeClass)
        {
            return EmitQualifiedCall(CoreNamespace + ".StyleAndClassNormalization.NormalizeClass", call.Arguments);
        }

        if (helper == HelperNames.NormalizeStyle)
        {
            return EmitQualifiedCall(CoreNamespace + ".StyleAndClassNormalization.NormalizeStyle", call.Arguments);
        }

        if (helper == HelperNames.ToDisplayString)
        {
            return EmitQualifiedCall(CoreNamespace + ".DisplayStringFormatter.ToDisplayString", call.Arguments);
        }

        if (helper == HelperNames.Camelize)
        {
            return EmitQualifiedCall(ComponentsNamespace + ".NameNormalization.Camelize", call.Arguments);
        }

        if (helper == HelperNames.WithModifiers)
        {
            return EmitQualifiedCall(BrowserNamespace + ".BrowserEvents.WithModifiers", call.Arguments);
        }

        if (helper == HelperNames.WithKeys)
        {
            return EmitQualifiedCall(BrowserNamespace + ".BrowserEvents.WithKeys", call.Arguments);
        }

        if (helper == HelperNames.ResolveDynamicComponent)
        {
            return call.Arguments.Count == 0
                ? CodeExpression.Literal("null")
                : EmitExpression(call.Arguments[0]);
        }

        if (helper == HelperNames.GuardReactiveProps)
        {
            return call.Arguments.Count == 0
                ? CodeExpression.Literal("null")
                : EmitExpression(call.Arguments[0]);
        }

        if (helper == HelperNames.NormalizeProps
            || helper == HelperNames.MergeProps
            || helper == HelperNames.ToHandlers)
        {
            return EmitPropertyDictionary(call, PropertyValuePurpose.Generic);
        }

        if (helper == HelperNames.RenderSlot
            || helper == HelperNames.WithMemo
            || helper == HelperNames.CreateText
            || helper == HelperNames.CreateComment
            || helper == HelperNames.CreateStatic)
        {
            return EmitCallAsVirtualValue(call);
        }

        if (helper == HelperNames.RenderList)
        {
            return EmitRenderList(call);
        }

        var expression = new CodeExpression();
        expression.Append(helper.Name);
        expression.Append("(");
        AppendCallArguments(expression, call.Arguments);
        expression.Append(")");
        return expression;
    }

    private CodeExpression EmitQualifiedCall(
        string name,
        IReadOnlyList<object> arguments)
    {
        var expression = new CodeExpression();
        expression.Append(name);
        expression.Append("(");
        AppendCallArguments(expression, arguments);
        expression.Append(")");
        return expression;
    }

    private void AppendCallArguments(
        CodeExpression expression,
        IReadOnlyList<object> arguments)
    {
        for (int index = 0; index < arguments.Count; index++)
        {
            if (index > 0)
            {
                expression.Append(", ");
            }

            expression.Append(EmitExpression(arguments[index]));
        }
    }

    private CodeExpression EmitArrayExpression(ArrayExpression array)
    {
        bool allStrings = true;
        for (int index = 0; index < array.Elements.Count; index++)
        {
            object element = array.Elements[index];
            if (element is SimpleExpressionNode { IsStatic: true })
            {
                continue;
            }

            if (element is string raw && IsQuotedString(raw))
            {
                continue;
            }

            allStrings = false;
            break;
        }

        var expression = new CodeExpression();
        expression.Append(allStrings ? "new string[] { " : "new object?[] { ");
        for (int index = 0; index < array.Elements.Count; index++)
        {
            if (index > 0)
            {
                expression.Append(", ");
            }

            expression.Append(EmitExpression(array.Elements[index]));
        }

        expression.Append(" }");
        return expression;
    }

    private CodeExpression EmitFunctionExpression(FunctionExpression function)
    {
        var expression = new CodeExpression();
        expression.Append("(");
        for (int index = 0; index < function.Parameters.Count; index++)
        {
            if (index > 0)
            {
                expression.Append(", ");
            }

            expression.Append(ParameterName(function.Parameters[index], "argument"));
        }

        expression.Append(") => ");
        if (function.Returns is not null)
        {
            expression.Append(EmitExpression(function.Returns));
            return expression;
        }

        expression.Append("{ }");
        return expression;
    }

    private CodeExpression EmitCompoundExpression(CompoundExpressionNode compound)
    {
        var expression = new CodeExpression();
        for (int index = 0; index < compound.Parts.Count; index++)
        {
            object part = compound.Parts[index];
            expression.Append(part switch
            {
                string raw => CodeExpression.Literal(MapRawLiteral(raw)),
                RuntimeHelper helper => EmitRuntimeHelperExpression(helper),
                _ => EmitExpression(part),
            });
        }

        return expression;
    }

    private CodeExpression EmitSimpleExpression(SimpleExpressionNode simple)
    {
        if (simple.IsStatic)
        {
            return CodeExpression.Literal(StringLiteral(simple.Content));
        }

        string text = MapRawLiteral(simple.Content);
        var expression = CodeExpression.Literal(text);
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

    private CodeExpression EmitDisplayString(object value)
    {
        var expression = new CodeExpression();
        expression.Append(CoreNamespace);
        expression.Append(".DisplayStringFormatter.ToDisplayString(");
        expression.Append(EmitExpression(value));
        expression.Append(")");
        return expression;
    }

    private CodeExpression EmitRuntimeHelperExpression(RuntimeHelper helper)
    {
        if (helper == HelperNames.ToDisplayString)
        {
            return CodeExpression.Literal(CoreNamespace + ".DisplayStringFormatter.ToDisplayString");
        }

        if (helper == HelperNames.Camelize)
        {
            return CodeExpression.Literal(ComponentsNamespace + ".NameNormalization.Camelize");
        }

        if (helper == HelperNames.WithModifiers)
        {
            return CodeExpression.Literal(BrowserNamespace + ".BrowserEvents.WithModifiers");
        }

        if (helper == HelperNames.WithKeys)
        {
            return CodeExpression.Literal(BrowserNamespace + ".BrowserEvents.WithKeys");
        }

        return CodeExpression.Literal(helper.Name);
    }

    private VirtualNodeTag ClassifyTag(object tag)
    {
        if (tag is RuntimeHelper helper)
        {
            if (helper == HelperNames.Fragment)
            {
                return new VirtualNodeTag(VirtualNodeTagKind.Fragment, CodeExpression.Literal("null"));
            }

            if (helper == HelperNames.Teleport)
            {
                return new VirtualNodeTag(VirtualNodeTagKind.Teleport, CodeExpression.Literal("null"));
            }

            if (helper == HelperNames.KeepAlive)
            {
                return new VirtualNodeTag(VirtualNodeTagKind.KeepAlive, CodeExpression.Literal("null"));
            }

            if (helper == HelperNames.Suspense)
            {
                return new VirtualNodeTag(VirtualNodeTagKind.Suspense, CodeExpression.Literal("null"));
            }

            if (helper == HelperNames.BaseTransition || helper == HelperNames.Transition)
            {
                return new VirtualNodeTag(VirtualNodeTagKind.Transition, CodeExpression.Literal("null"));
            }

            if (helper == HelperNames.TransitionGroup)
            {
                return new VirtualNodeTag(
                    VirtualNodeTagKind.Component,
                    ComponentReferenceForName("TransitionGroup"));
            }
        }

        if (tag is CallExpression call
            && call.Callee is RuntimeHelper callHelper
            && callHelper == HelperNames.ResolveDynamicComponent)
        {
            CodeExpression selector = call.Arguments.Count == 0
                ? CodeExpression.Literal("null")
                : EmitExpression(call.Arguments[0]);
            return new VirtualNodeTag(VirtualNodeTagKind.Dynamic, selector);
        }

        if (tag is string raw)
        {
            string? componentName = ResolveAssetName(raw, "component", result.Components);
            if (componentName is not null)
            {
                return new VirtualNodeTag(
                    VirtualNodeTagKind.Component,
                    ComponentReferenceForName(componentName));
            }

            return new VirtualNodeTag(
                VirtualNodeTagKind.Element,
                CodeExpression.Literal(MapRawLiteral(raw)));
        }

        throw new InvalidOperationException(
            $"Unsupported virtual-node tag '{tag.GetType().Name}'.");
    }

    private static CodeExpression ComponentReferenceForName(string name)
    {
        var expression = new CodeExpression();
        expression.Append(ComponentsNamespace);
        expression.Append(".ComponentReference.ForName(");
        expression.Append(StringLiteral(name));
        expression.Append(")");
        return expression;
    }

    private static string? ResolveAssetName(
        string generatedIdentifier,
        string type,
        IReadOnlyList<string> names)
    {
        for (int index = 0; index < names.Count; index++)
        {
            string name = names[index];
            if (string.Equals(
                generatedIdentifier,
                TransformElement.ToValidAssetId(name, type),
                StringComparison.Ordinal))
            {
                return name;
            }
        }

        return null;
    }

    private CodeExpression EmitDynamicBindingIndices(
        string bindingsName,
        IReadOnlyList<string> dynamicPropertyNames)
    {
        if (dynamicPropertyNames.Count == 0)
        {
            return CodeExpression.Literal("null");
        }

        string indicesName = NextName("dynamicBindingIndices");
        string indexName = NextName("bindingIndex");
        AppendStatement(
            $"var {indicesName} = new global::System.Collections.Generic.List<int>();");
        AppendLine($"for (int {indexName} = 0; {indexName} < {bindingsName}.Count; {indexName}++)");
        AppendLine("{");
        indentLevel++;
        BeginLine();
        Push("if (");
        for (int index = 0; index < dynamicPropertyNames.Count; index++)
        {
            if (index > 0)
            {
                Push(" || ");
            }

            Push("global::System.String.Equals(");
            Push(bindingsName);
            Push("[");
            Push(indexName);
            Push("].Name.LocalName, ");
            Push(StringLiteral(dynamicPropertyNames[index]));
            Push(", global::System.StringComparison.Ordinal)");
        }

        Push(")");
        EndLine();
        AppendLine("{");
        indentLevel++;
        AppendStatement($"{indicesName}.Add({indexName});");
        indentLevel--;
        AppendLine("}");
        indentLevel--;
        AppendLine("}");
        return CodeExpression.Literal(indicesName);
    }

    private static IReadOnlyList<string> ParseDynamicPropertyNames(object? node)
    {
        string? text = node switch
        {
            string raw => raw,
            SimpleExpressionNode simple => simple.Content,
            _ => null,
        };
        if (text is null || text.Length == 0)
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        bool insideString = false;
        var current = new StringBuilder();
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '"')
            {
                if (insideString)
                {
                    names.Add(current.ToString());
                    current.Clear();
                }

                insideString = !insideString;
            }
            else if (insideString)
            {
                current.Append(character);
            }
        }

        return names;
    }

    private static int ParseRawInteger(object? node)
    {
        string text = node switch
        {
            string raw => raw,
            SimpleExpressionNode simple => simple.Content,
            _ => "0",
        };
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : 0;
    }

    private static string ParameterName(object parameter, string fallback)
    {
        string name = parameter switch
        {
            SimpleExpressionNode simple => simple.Content,
            string raw => raw,
            _ => fallback,
        };
        name = MapIdentifier(name);
        if (name.Length == 0 || name[0] == '_')
        {
            return fallback;
        }

        return name;
    }

    private static string MapIdentifier(string name)
    {
        if (name == "$event")
        {
            return "eventValue";
        }

        return name;
    }

    private static string MapRawLiteral(string raw)
    {
        switch (raw)
        {
            case "undefined":
            case "void 0":
                return "null";
            case "$slots":
                return "component.Context!.Bindings.Slots";
            case "{}":
                return "new global::System.Collections.Generic.Dictionary<string, object?>()";
        }

        string mapped = raw;
        if (mapped.StartsWith("const ", StringComparison.Ordinal))
        {
            mapped = "var " + mapped.Substring(6);
        }

        mapped = CompilerText.ReplaceIdentifierToken(mapped, "_ctx", "component", replaceMemberAccess: false);
        mapped = CompilerText.ReplaceIdentifierToken(mapped, "$event", "eventValue", replaceMemberAccess: false);
        mapped = CompilerText.ReplaceIdentifierToken(mapped, "__event", "eventValue", replaceMemberAccess: false);
        mapped = CompilerText.ReplaceIdentifierToken(
            mapped,
            "_unref",
            GeneratedNamespace + ".RenderGlue.Unwrap",
            replaceMemberAccess: false);
        mapped = CompilerText.ReplaceIdentifierToken(
            mapped,
            "_camelize",
            ComponentsNamespace + ".NameNormalization.Camelize",
            replaceMemberAccess: false);
        mapped = CompilerText.ReplaceIdentifierToken(
            mapped,
            "_toDisplayString",
            CoreNamespace + ".DisplayStringFormatter.ToDisplayString",
            replaceMemberAccess: false);
        mapped = CompilerText.ReplaceIdentifierToken(
            mapped,
            "_toHandlerKey",
            "\"on\" + " + ComponentsNamespace + ".NameNormalization.Pascalize",
            replaceMemberAccess: false);
        mapped = ReplaceCompilerCachedKeyAccess(mapped);
        mapped = CompilerText.ReplaceIdentifierToken(mapped, "_cache", "frame.Cache", replaceMemberAccess: false);
        mapped = CompilerText.ReplaceIdentifierToken(mapped, "_cached", "cachedNode", replaceMemberAccess: false);
        mapped = CompilerText.ReplaceIdentifierToken(mapped, "_memo", "memoDependencies", replaceMemberAccess: false);
        mapped = CompilerText.ReplaceIdentifierToken(mapped, "_item", "memoizedItem", replaceMemberAccess: false);
        mapped = mapped.Replace(" === ", " == ");
        mapped = mapped.Replace(" !== ", " != ");
        return mapped;
    }

    private static string ReplaceCompilerCachedKeyAccess(string text)
    {
        const string compilerAccess = "_cached.key";
        var searchStart = 0;
        var copyStart = 0;
        StringBuilder? builder = null;
        while (searchStart < text.Length)
        {
            var index = text.IndexOf(compilerAccess, searchStart, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            var end = index + compilerAccess.Length;
            var hasIdentifierBefore = index > 0 &&
                (SyntaxFacts.IsIdentifierPartCharacter(text[index - 1]) || text[index - 1] is '@' or '.');
            var hasIdentifierAfter = end < text.Length && SyntaxFacts.IsIdentifierPartCharacter(text[end]);
            if (!hasIdentifierBefore && !hasIdentifierAfter)
            {
                builder ??= new StringBuilder(text.Length);
                builder.Append(text, copyStart, index - copyStart);
                builder.Append("cachedNode.Key");
                copyStart = end;
            }

            searchStart = end;
        }

        if (builder is null)
        {
            return text;
        }

        builder.Append(text, copyStart, text.Length - copyStart);
        return builder.ToString();
    }

    private string MaterializeExpression(string prefix, CodeExpression expression)
    {
        string name = NextName(prefix);
        BeginLine();
        Push("var ");
        Push(name);
        Push(" = ");
        AppendExpression(expression);
        Push(";");
        EndLine();
        return name;
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

    private void AppendStatement(string statement) => AppendLine(statement);

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

    private static bool IsGeneratedEventName(string name) =>
        name.Length > 2
        && name[0] == 'o'
        && name[1] == 'n'
        && !char.IsLower(name[2]);

    private static string GeneratedEventNameTest(string expression) =>
        $"{expression}.Length > 2"
        + $" && {expression}[0] == 'o'"
        + $" && {expression}[1] == 'n'"
        + $" && !global::System.Char.IsLower({expression}[2])";

    private static string GeneratedEventNameValue(string expression) =>
        $"global::System.Char.ToLowerInvariant({expression}[2]).ToString() + {expression}.Substring(3)";

    private static string GeneratedHostPropertyNameTest(string expression) =>
        $"global::System.String.Equals({expression}, \"value\", global::System.StringComparison.Ordinal)"
        + $" || global::System.String.Equals({expression}, \"checked\", global::System.StringComparison.Ordinal)"
        + $" || global::System.String.Equals({expression}, \"selected\", global::System.StringComparison.Ordinal)"
        + $" || global::System.String.Equals({expression}, \"muted\", global::System.StringComparison.Ordinal)"
        + $" || global::System.String.Equals({expression}, \"innerHTML\", global::System.StringComparison.Ordinal)"
        + $" || global::System.String.Equals({expression}, \"textContent\", global::System.StringComparison.Ordinal)";

    private static bool IsQuotedString(string value) =>
        value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"';

    private static string StringLiteral(string value) =>
        SymbolDisplay.FormatLiteral(value, quote: true);

    private enum PropertyValuePurpose
    {
        Generic,
        Element,
        Component,
    }

    private enum VirtualNodeTagKind
    {
        Element,
        Fragment,
        Teleport,
        KeepAlive,
        Suspense,
        Transition,
        Component,
        Dynamic,
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

    private sealed class ElementPropertyEmission
    {
        internal ElementPropertyEmission(
            string propertiesName,
            string bindingsName,
            string keyName,
            string mountReferenceName)
        {
            PropertiesName = propertiesName;
            BindingsName = bindingsName;
            KeyName = keyName;
            MountReferenceName = mountReferenceName;
        }

        internal string PropertiesName { get; }

        internal string BindingsName { get; }

        internal string KeyName { get; }

        internal string MountReferenceName { get; }
    }

    private sealed class GenericPropertyEmission
    {
        internal GenericPropertyEmission(
            string propertiesName,
            string keyName,
            string mountReferenceName)
        {
            PropertiesName = propertiesName;
            KeyName = keyName;
            MountReferenceName = mountReferenceName;
        }

        internal string PropertiesName { get; }

        internal string KeyName { get; }

        internal string MountReferenceName { get; }
    }

    private sealed class ComponentInvocationEmission
    {
        internal ComponentInvocationEmission(
            string invocationName,
            string keyName,
            string mountReferenceName)
        {
            InvocationName = invocationName;
            KeyName = keyName;
            MountReferenceName = mountReferenceName;
        }

        internal string InvocationName { get; }

        internal string KeyName { get; }

        internal string MountReferenceName { get; }
    }

    private sealed class SlotEmission
    {
        internal SlotEmission(string slotsName, int stability)
        {
            SlotsName = slotsName;
            Stability = stability;
        }

        internal string SlotsName { get; }

        internal int Stability { get; }
    }

    private sealed class VirtualNodeTag
    {
        internal VirtualNodeTag(VirtualNodeTagKind kind, CodeExpression value)
        {
            Kind = kind;
            Value = value;
        }

        internal VirtualNodeTagKind Kind { get; }

        internal CodeExpression Value { get; }
    }
}
