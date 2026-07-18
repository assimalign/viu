using System;
using System.Collections.Generic;
using System.Text;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// The element transform: on exit it builds the element's <see cref="VNodeCall"/> code-generation node,
/// resolving the vnode tag (including dynamic components), building its props, and attaching its children or
/// slots. The C# port of Vue 3.5's <c>transformElement</c>, <c>resolveComponentType</c>, <c>buildProps</c>,
/// and <c>buildDirectiveArgs</c> (<c>@vue/compiler-core</c> <c>transforms/transformElement.ts</c>).
/// </summary>
/// <remarks>
/// The patch-flag analysis in <see cref="BuildProps"/> is ported here because it is inseparable from prop
/// building and because [V01.01.05.03] requires the <c>FULL_PROPS</c> escalation and the <c>dynamicProps</c>
/// list. The full element-level patch-flag refinement (static class/style elision, text) is [V01.01.05.06].
/// </remarks>
internal static class TransformElement
{
    /// <summary>The node transform (work happens on exit, after children are transformed).</summary>
    public static Action? Transform(SyntaxNode node, TransformContext context) => () =>
    {
        var current = context.CurrentNode;
        if (current is not ElementNode element ||
            (element.ElementType != ElementType.Element && element.ElementType != ElementType.Component))
        {
            return;
        }

        var tag = element.Tag;
        var isComponent = element.ElementType == ElementType.Component;

        object vnodeTag = isComponent ? ResolveComponentType(element, context) : $"\"{tag}\"";
        var isDynamicComponent = vnodeTag is CallExpression { Callee: RuntimeHelper dynamicCallee } &&
                                 dynamicCallee == HelperNames.ResolveDynamicComponent;

        var shouldUseBlock =
            isDynamicComponent ||
            (vnodeTag is RuntimeHelper tagHelper && (tagHelper == HelperNames.Teleport || tagHelper == HelperNames.Suspense)) ||
            (!isComponent && (tag is "svg" or "foreignObject" or "math"));

        SyntaxNode? vnodeProps = null;
        object? vnodeChildren = null;
        PatchFlags patchFlag = 0;
        object? vnodeDynamicProps = null;
        IReadOnlyList<string>? dynamicPropNames = null;
        ArrayExpression? vnodeDirectives = null;

        if (element.Properties.Count > 0)
        {
            var built = BuildProps(element, context, element.Properties, isComponent, isDynamicComponent);
            vnodeProps = built.Props;
            patchFlag = built.PatchFlag;
            dynamicPropNames = built.DynamicPropNames;
            if (built.Directives.Count > 0)
            {
                var directiveArguments = new object[built.Directives.Count];
                for (var index = 0; index < built.Directives.Count; index++)
                {
                    directiveArguments[index] = BuildDirectiveArguments(built.Directives[index], context);
                }

                vnodeDirectives = Ir.ArrayExpression(directiveArguments);
            }

            if (built.ShouldUseBlock)
            {
                shouldUseBlock = true;
            }
        }

        var children = context.WorkingChildrenOf(element, element.Children);
        if (children.Count > 0)
        {
            if (vnodeTag is RuntimeHelper keepAlive && keepAlive == HelperNames.KeepAlive)
            {
                shouldUseBlock = true;
                patchFlag |= PatchFlags.DynamicSlots;
                if (children.Count > 1)
                {
                    context.ReportError(CompilerErrorFactory.Create(
                        CompilerErrorCode.XKeepAliveInvalidChildren,
                        new SourceLocation(children[0].Location.Start, children[children.Count - 1].Location.End, string.Empty)));
                }
            }

            var shouldBuildAsSlots = isComponent &&
                                     !(vnodeTag is RuntimeHelper teleport && teleport == HelperNames.Teleport) &&
                                     !(vnodeTag is RuntimeHelper keep && keep == HelperNames.KeepAlive);

            if (shouldBuildAsSlots)
            {
                var (slots, hasDynamicSlots) = VSlotTransform.BuildSlots(element, context, children);
                vnodeChildren = slots;
                if (hasDynamicSlots)
                {
                    patchFlag |= PatchFlags.DynamicSlots;
                }
            }
            else if (children.Count == 1 && !(vnodeTag is RuntimeHelper t && t == HelperNames.Teleport))
            {
                var child = children[0];
                var hasDynamicTextChild = child is InterpolationNode or CompoundExpressionNode;
                if (hasDynamicTextChild && ConstantAnalysis.GetConstantType(child) == ConstantType.NotConstant)
                {
                    patchFlag |= PatchFlags.Text;
                }

                vnodeChildren = hasDynamicTextChild || child is TextNode
                    ? child
                    : TransformFreeze.FreezeChildren(children);
            }
            else
            {
                vnodeChildren = TransformFreeze.FreezeChildren(children);
            }
        }

        if (dynamicPropNames is { Count: > 0 })
        {
            vnodeDynamicProps = StringifyDynamicPropNames(dynamicPropNames);
        }

        var codegenNode = context.CreateVNodeCall(
            vnodeTag,
            vnodeProps,
            vnodeChildren,
            patchFlag == 0 ? null : patchFlag,
            vnodeDynamicProps,
            vnodeDirectives,
            shouldUseBlock,
            disableTracking: false,
            isComponent,
            element.Location);

        context.SetCodegenNode(element, codegenNode);
    };

    /// <summary>Resolves the vnode tag for a component, including dynamic components (upstream <c>resolveComponentType</c>).</summary>
    public static object ResolveComponentType(ElementNode element, TransformContext context)
    {
        var tag = element.Tag;
        var isExplicitDynamic = IsComponentTag(tag);
        var isProperty = TransformUtilities.FindProperty(element, "is", dynamicOnly: false, allowEmpty: true);
        if (isProperty is not null)
        {
            if (isExplicitDynamic)
            {
                ExpressionNode? expression = null;
                if (isProperty is AttributeNode attribute)
                {
                    expression = attribute.Value is not null ? Ir.SimpleExpression(attribute.Value.Content, true) : null;
                }
                else if (isProperty is DirectiveNode directive)
                {
                    expression = directive.Expression ?? Ir.SimpleExpression("is", false, directive.Argument!.Location);
                }

                if (expression is not null)
                {
                    return Ir.CallExpression(context.Helper(HelperNames.ResolveDynamicComponent), new object[] { expression });
                }
            }
            else if (isProperty is AttributeNode { Value: { } value } && value.Content.StartsWith("vue:", StringComparison.Ordinal))
            {
                tag = value.Content.Substring(4);
            }
        }

        var builtIn = CoreComponent(tag) ?? context.IsBuiltInComponent?.Invoke(tag);
        if (builtIn is not null)
        {
            if (!context.Ssr)
            {
                context.Helper(builtIn);
            }

            return builtIn;
        }

        context.Helper(HelperNames.ResolveComponent);
        context.AddComponent(tag);
        return ToValidAssetId(tag, "component");
    }

    /// <summary>Builds an element's props, directives, and patch-flag analysis (upstream <c>buildProps</c>).</summary>
    public static BuildPropsResult BuildProps(
        ElementNode element,
        TransformContext context,
        SyntaxList<PropertyNode> properties,
        bool isComponent,
        bool isDynamicComponent)
    {
        var tag = element.Tag;
        var hasChildren = element.Children.Count > 0;
        var elementProperties = new List<Property>();
        var mergeArguments = new List<SyntaxNode>();
        var runtimeDirectives = new List<DirectiveNode>();
        var shouldUseBlock = false;

        var patchFlag = 0;
        var hasReference = false;
        var hasClassBinding = false;
        var hasStyleBinding = false;
        var hasHydrationEventBinding = false;
        var hasDynamicKeys = false;
        var hasVnodeHook = false;
        var dynamicPropNames = new List<string>();

        void PushMergeArgument(SyntaxNode? argument = null)
        {
            if (elementProperties.Count > 0)
            {
                mergeArguments.Add(Ir.ObjectExpression(DedupeProperties(elementProperties), element.Location));
                elementProperties = new List<Property>();
            }

            if (argument is not null)
            {
                mergeArguments.Add(argument);
            }
        }

        void PushReferenceForVForMarker()
        {
            if (context.ScopeVFor > 0)
            {
                elementProperties.Add(Ir.ObjectProperty(
                    Ir.SimpleExpression("ref_for", true),
                    Ir.SimpleExpression("true")));
            }
        }

        void AnalyzePatchFlag(Property property)
        {
            var key = property.Key;
            var value = property.Value;
            if (key is SimpleExpressionNode { IsStatic: true } staticKey)
            {
                var name = staticKey.Content;
                var isEventHandler = CompilerText.IsOn(name);
                if (isEventHandler && (!isComponent || isDynamicComponent) &&
                    name.ToLowerInvariant() != "onclick" &&
                    name != "onUpdate:modelValue" &&
                    !CompilerText.IsReservedProperty(name))
                {
                    hasHydrationEventBinding = true;
                }

                if (isEventHandler && CompilerText.IsReservedProperty(name))
                {
                    hasVnodeHook = true;
                }

                if (isEventHandler && value is CallExpression handlerCall && handlerCall.Arguments.Count > 0 &&
                    handlerCall.Arguments[0] is SyntaxNode innerValue)
                {
                    value = innerValue;
                }

                if (value is CacheExpression ||
                    ((value is SimpleExpressionNode or CompoundExpressionNode) &&
                     ConstantAnalysis.GetConstantType(value) > ConstantType.NotConstant))
                {
                    return;
                }

                if (name == "ref")
                {
                    hasReference = true;
                }
                else if (name == "class")
                {
                    hasClassBinding = true;
                }
                else if (name == "style")
                {
                    hasStyleBinding = true;
                }
                else if (name != "key" && !dynamicPropNames.Contains(name))
                {
                    dynamicPropNames.Add(name);
                }

                if (isComponent && (name == "class" || name == "style") && !dynamicPropNames.Contains(name))
                {
                    dynamicPropNames.Add(name);
                }
            }
            else
            {
                hasDynamicKeys = true;
            }
        }

        foreach (var property in properties)
        {
            if (property is AttributeNode attribute)
            {
                var name = attribute.Name;
                if (name == "ref")
                {
                    hasReference = true;
                    PushReferenceForVForMarker();
                }

                if (name == "is" && (IsComponentTag(tag) || (attribute.Value is not null && attribute.Value.Content.StartsWith("vue:", StringComparison.Ordinal))))
                {
                    continue;
                }

                elementProperties.Add(Ir.ObjectProperty(
                    Ir.SimpleExpression(name, true, attribute.NameLocation),
                    Ir.SimpleExpression(attribute.Value?.Content ?? string.Empty, true, attribute.Value?.Location ?? attribute.Location)));
            }
            else if (property is DirectiveNode directive)
            {
                var name = directive.Name;
                var argument = directive.Argument;
                var expression = directive.Expression;
                var isVBind = name == "bind";
                var isVOn = name == "on";

                if (name == "slot")
                {
                    if (!isComponent)
                    {
                        context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVSlotMisplaced, directive.Location));
                    }

                    continue;
                }

                if (name is "once" or "memo")
                {
                    continue;
                }

                if (name == "is" || (isVBind && TransformUtilities.IsStaticArgumentOf(argument, "is") && IsComponentTag(tag)))
                {
                    continue;
                }

                if (isVOn && context.Ssr)
                {
                    continue;
                }

                if ((isVBind && TransformUtilities.IsStaticArgumentOf(argument, "key")) ||
                    (isVOn && hasChildren && TransformUtilities.IsStaticArgumentOf(argument, "vue:before-update")))
                {
                    shouldUseBlock = true;
                }

                if (isVBind && TransformUtilities.IsStaticArgumentOf(argument, "ref"))
                {
                    PushReferenceForVForMarker();
                }

                if (argument is null && (isVBind || isVOn))
                {
                    hasDynamicKeys = true;
                    if (expression is not null)
                    {
                        if (isVBind)
                        {
                            PushReferenceForVForMarker();
                            PushMergeArgument();
                            mergeArguments.Add(expression);
                        }
                        else
                        {
                            PushMergeArgument(Ir.CallExpression(
                                context.Helper(HelperNames.ToHandlers),
                                isComponent ? new object[] { expression } : new object[] { expression, "true" }));
                        }
                    }
                    else
                    {
                        context.ReportError(CompilerErrorFactory.Create(
                            isVBind ? CompilerErrorCode.XVBindNoExpression : CompilerErrorCode.XVOnNoExpression,
                            directive.Location));
                    }

                    continue;
                }

                if (isVBind && HasModifier(directive, "prop"))
                {
                    patchFlag |= (int)PatchFlags.NeedHydration;
                }

                if (context.DirectiveTransforms.TryGetValue(name, out var directiveTransform))
                {
                    var result = directiveTransform(directive, element, context, null);
                    if (!context.Ssr)
                    {
                        foreach (var built in result.Properties)
                        {
                            AnalyzePatchFlag(built);
                        }
                    }

                    if (isVOn && argument is not null && !TransformUtilities.IsStaticExpression(argument))
                    {
                        PushMergeArgument(Ir.ObjectExpression(result.Properties, element.Location));
                    }
                    else
                    {
                        elementProperties.AddRange(result.Properties);
                    }

                    if (result.NeedRuntime is not null || result.NeedsResolvedDirective)
                    {
                        runtimeDirectives.Add(directive);
                        if (result.NeedRuntime is not null)
                        {
                            context.SetDirectiveRuntime(directive, result.NeedRuntime);
                        }
                    }
                }
                else if (!CompilerText.IsBuiltInDirective(name))
                {
                    runtimeDirectives.Add(directive);
                    if (hasChildren)
                    {
                        shouldUseBlock = true;
                    }
                }
            }
        }

        SyntaxNode? propsExpression = null;
        if (mergeArguments.Count > 0)
        {
            PushMergeArgument();
            propsExpression = mergeArguments.Count > 1
                ? Ir.CallExpression(context.Helper(HelperNames.MergeProps), ToObjectList(mergeArguments), element.Location)
                : mergeArguments[0];
        }
        else if (elementProperties.Count > 0)
        {
            propsExpression = Ir.ObjectExpression(DedupeProperties(elementProperties), element.Location);
        }

        if (hasDynamicKeys)
        {
            patchFlag |= (int)PatchFlags.FullProps;
        }
        else
        {
            if (hasClassBinding && !isComponent)
            {
                patchFlag |= (int)PatchFlags.Class;
            }

            if (hasStyleBinding && !isComponent)
            {
                patchFlag |= (int)PatchFlags.Style;
            }

            if (dynamicPropNames.Count > 0)
            {
                patchFlag |= (int)PatchFlags.Props;
            }

            if (hasHydrationEventBinding)
            {
                patchFlag |= (int)PatchFlags.NeedHydration;
            }
        }

        if (!shouldUseBlock &&
            (patchFlag == 0 || patchFlag == (int)PatchFlags.NeedHydration) &&
            (hasReference || hasVnodeHook || runtimeDirectives.Count > 0))
        {
            patchFlag |= (int)PatchFlags.NeedPatch;
        }

        if (!context.InSSR && propsExpression is not null)
        {
            propsExpression = NormalizeProps(propsExpression, context, hasStyleBinding);
        }

        return new BuildPropsResult
        {
            Props = propsExpression,
            Directives = runtimeDirectives,
            PatchFlag = (PatchFlags)patchFlag,
            DynamicPropNames = dynamicPropNames,
            ShouldUseBlock = shouldUseBlock,
        };
    }

    /// <summary>Builds the runtime directive argument array for a custom/model directive (upstream <c>buildDirectiveArgs</c>).</summary>
    public static ArrayExpression BuildDirectiveArguments(DirectiveNode directive, TransformContext context)
    {
        var arguments = new List<object>();
        var runtime = context.GetDirectiveRuntime(directive);
        if (runtime is not null)
        {
            arguments.Add(context.HelperString(runtime));
        }
        else
        {
            context.Helper(HelperNames.ResolveDirective);
            context.AddDirective(directive.Name);
            arguments.Add(ToValidAssetId(directive.Name, "directive"));
        }

        if (directive.Expression is not null)
        {
            arguments.Add(directive.Expression);
        }

        if (directive.Argument is not null)
        {
            if (directive.Expression is null)
            {
                arguments.Add("void 0");
            }

            arguments.Add(directive.Argument);
        }

        if (directive.Modifiers.Count > 0)
        {
            if (directive.Argument is null)
            {
                if (directive.Expression is null)
                {
                    arguments.Add("void 0");
                }

                arguments.Add("void 0");
            }

            var trueExpression = Ir.SimpleExpression("true", false, directive.Location);
            var modifierProperties = new List<Property>();
            foreach (var modifier in directive.Modifiers)
            {
                modifierProperties.Add(Ir.ObjectProperty(modifier, trueExpression));
            }

            arguments.Add(Ir.ObjectExpression(modifierProperties, directive.Location));
        }

        return Ir.ArrayExpression(arguments, directive.Location);
    }

    private static SyntaxNode NormalizeProps(SyntaxNode propsExpression, TransformContext context, bool hasStyleBinding)
    {
        switch (propsExpression)
        {
            case ObjectExpression objectExpression:
                var classIndex = -1;
                var styleIndex = -1;
                var hasDynamicKey = false;
                for (var index = 0; index < objectExpression.Properties.Count; index++)
                {
                    var key = objectExpression.Properties[index].Key;
                    if (key is SimpleExpressionNode { IsStatic: true } staticKey)
                    {
                        if (staticKey.Content == "class")
                        {
                            classIndex = index;
                        }
                        else if (staticKey.Content == "style")
                        {
                            styleIndex = index;
                        }
                    }
                    else if (!(key is SimpleExpressionNode { IsHandlerKey: true }) &&
                             !(key is CompoundExpressionNode { IsHandlerKey: true }))
                    {
                        hasDynamicKey = true;
                    }
                }

                if (!hasDynamicKey)
                {
                    var updated = new List<Property>(objectExpression.Properties);
                    if (classIndex >= 0 && !TransformUtilities.IsStaticExpression(updated[classIndex].Value))
                    {
                        updated[classIndex] = updated[classIndex] with
                        {
                            Value = Ir.CallExpression(context.Helper(HelperNames.NormalizeClass), new object[] { updated[classIndex].Value }),
                        };
                    }

                    if (styleIndex >= 0)
                    {
                        var styleProperty = updated[styleIndex];
                        var isDynamicStyle = hasStyleBinding ||
                                             (styleProperty.Value is SimpleExpressionNode s && s.Content.TrimStart().StartsWith("[", StringComparison.Ordinal)) ||
                                             styleProperty.Value is ArrayExpression;
                        if (isDynamicStyle)
                        {
                            updated[styleIndex] = styleProperty with
                            {
                                Value = Ir.CallExpression(context.Helper(HelperNames.NormalizeStyle), new object[] { styleProperty.Value }),
                            };
                        }
                    }

                    return objectExpression with { Properties = new SyntaxList<Property>(updated.ToArray()) };
                }

                return Ir.CallExpression(context.Helper(HelperNames.NormalizeProps), new object[] { objectExpression });
            case CallExpression:
                return propsExpression;
            default:
                return Ir.CallExpression(
                    context.Helper(HelperNames.NormalizeProps),
                    new object[] { Ir.CallExpression(context.Helper(HelperNames.GuardReactiveProps), new object[] { propsExpression }) });
        }
    }

    private static List<Property> DedupeProperties(List<Property> properties)
    {
        var known = new Dictionary<string, Property>();
        var deduped = new List<Property>();
        foreach (var property in properties)
        {
            if (property.Key is CompoundExpressionNode || property.Key is SimpleExpressionNode { IsStatic: false })
            {
                deduped.Add(property);
                continue;
            }

            var name = ((SimpleExpressionNode)property.Key).Content;
            if (known.TryGetValue(name, out var existing))
            {
                if (name is "style" or "class" || CompilerText.IsOn(name))
                {
                    var merged = MergeAsArray(existing, property);
                    var replaceIndex = deduped.IndexOf(existing);
                    if (replaceIndex >= 0)
                    {
                        deduped[replaceIndex] = merged;
                    }

                    known[name] = merged;
                }
            }
            else
            {
                known[name] = property;
                deduped.Add(property);
            }
        }

        return deduped;
    }

    private static Property MergeAsArray(Property existing, Property incoming)
    {
        if (existing.Value is ArrayExpression array)
        {
            var elements = new List<object>(array.Elements) { incoming.Value };
            return existing with { Value = array with { Elements = new SyntaxList<object>(elements.ToArray()) } };
        }

        return existing with { Value = Ir.ArrayExpression(new object[] { existing.Value, incoming.Value }, existing.Location) };
    }

    private static bool HasModifier(DirectiveNode directive, string modifier)
    {
        foreach (var entry in directive.Modifiers)
        {
            if (entry.Content == modifier)
            {
                return true;
            }
        }

        return false;
    }

    private static RuntimeHelper? CoreComponent(string tag) => tag switch
    {
        "Teleport" or "teleport" => HelperNames.Teleport,
        "Suspense" or "suspense" => HelperNames.Suspense,
        "KeepAlive" or "keep-alive" => HelperNames.KeepAlive,
        "BaseTransition" or "base-transition" => HelperNames.BaseTransition,
        _ => null,
    };

    private static bool IsComponentTag(string tag) => tag is "component" or "Component";

    private static string ToValidAssetId(string name, string type)
    {
        var builder = new StringBuilder("_").Append(type).Append('_');
        foreach (var character in name)
        {
            var isWord = (character >= 'A' && character <= 'Z') ||
                         (character >= 'a' && character <= 'z') ||
                         (character >= '0' && character <= '9') ||
                         character == '_';
            if (isWord)
            {
                builder.Append(character);
            }
            else if (character == '-')
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(((int)character).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static string StringifyDynamicPropNames(IReadOnlyList<string> names)
    {
        var builder = new StringBuilder("[");
        for (var index = 0; index < names.Count; index++)
        {
            builder.Append('"').Append(names[index]).Append('"');
            if (index < names.Count - 1)
            {
                builder.Append(", ");
            }
        }

        return builder.Append(']').ToString();
    }

    private static IReadOnlyList<object> ToObjectList(List<SyntaxNode> nodes)
    {
        var array = new object[nodes.Count];
        for (var index = 0; index < nodes.Count; index++)
        {
            array[index] = nodes[index];
        }

        return array;
    }
}
