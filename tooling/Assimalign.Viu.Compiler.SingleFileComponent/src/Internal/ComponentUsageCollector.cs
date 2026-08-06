using System;
using System.Collections.Generic;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// Walks a parsed template and records every component usage as the value-equatable manifest the
/// build-time checker consumes ([SFC-USE-1]). It resolves nothing: which components exist, and what
/// they declare, is a compilation-wide question the host answers, so the per-file projection stays a
/// pure function of the file and keeps its incremental cache.
/// <para>
/// The reduction is deliberately conservative. A usage that can supply arguments this manifest cannot
/// enumerate — an argument-less <c>v-bind="…"</c> spread or a dynamic <c>:[name]</c> argument — is
/// flagged <see cref="ComponentUsage.SuppliesUnknownArguments"/> so the checker stays silent about it
/// entirely, and a bound value's type is recorded only when the expression is itself a C# literal.
/// </para>
/// </summary>
internal static class ComponentUsageCollector
{
    /// <summary>Collects the component usages in <paramref name="root"/>.</summary>
    /// <param name="root">The parsed template root.</param>
    /// <param name="filePath">The originating single-file-component path.</param>
    /// <param name="blockContentStart">The file position where the template block's content begins.</param>
    /// <returns>The usages in source order, or an empty array when the template uses no component.</returns>
    public static EquatableArray<ComponentUsage> Collect(
        RootNode root,
        string filePath,
        Position blockContentStart)
    {
        List<ComponentUsage>? usages = null;
        Visit(root.Children, filePath, blockContentStart, ref usages);
        return usages is null
            ? EquatableArray<ComponentUsage>.Empty
            : new EquatableArray<ComponentUsage>(usages.ToArray());
    }

    private static void Visit(
        IReadOnlyList<TemplateChildNode> children,
        string filePath,
        Position blockContentStart,
        ref List<ComponentUsage>? usages)
    {
        foreach (var child in children)
        {
            if (child is not ElementNode element)
            {
                continue;
            }

            if (element.ElementType == ElementType.Component)
            {
                (usages ??= new List<ComponentUsage>()).Add(
                    Describe(element, filePath, blockContentStart));
            }

            Visit(element.Children, filePath, blockContentStart, ref usages);
        }
    }

    private static ComponentUsage Describe(
        ElementNode element,
        string filePath,
        Position blockContentStart)
    {
        var properties = new List<ComponentUsageProperty>(element.Properties.Count);
        var suppliesUnknownArguments = false;

        foreach (var property in element.Properties)
        {
            switch (property)
            {
                case AttributeNode attribute:
                    properties.Add(new ComponentUsageProperty(
                        ComponentDeclarationNames.Canonicalize(attribute.Name),
                        attribute.Name,
                        IsListenerName(attribute.Name)
                            ? ComponentUsagePropertyKind.Listener
                            : ComponentUsagePropertyKind.Static,
                        // A plain attribute's value is always a string literal, and a valueless
                        // attribute compiles to the empty string — the one form whose supplied type is
                        // never in doubt.
                        ComponentValueKind.Text,
                        Compose(filePath, blockContentStart, attribute.NameLocation)));
                    break;

                case DirectiveNode directive:
                    DescribeDirective(
                        directive,
                        filePath,
                        blockContentStart,
                        properties,
                        ref suppliesUnknownArguments);
                    break;
            }
        }

        return new ComponentUsage(
            element.Tag,
            TagNameLocation(element, filePath, blockContentStart),
            properties.Count == 0
                ? EquatableArray<ComponentUsageProperty>.Empty
                : new EquatableArray<ComponentUsageProperty>(properties.ToArray()),
            suppliesUnknownArguments);
    }

    private static void DescribeDirective(
        DirectiveNode directive,
        string filePath,
        Position blockContentStart,
        List<ComponentUsageProperty> properties,
        ref bool suppliesUnknownArguments)
    {
        switch (directive.Name)
        {
            case "bind":
            {
                if (directive.Argument is null)
                {
                    // `v-bind="object"` spreads an object of unknown shape over the component's
                    // arguments. Nothing about this usage's supplied set is decidable any more.
                    suppliesUnknownArguments = true;
                    return;
                }

                if (directive.Argument is not SimpleExpressionNode { IsStatic: true } argument)
                {
                    // A dynamic argument (`:[name]="value"`) names a parameter only at run time.
                    suppliesUnknownArguments = true;
                    return;
                }

                properties.Add(new ComponentUsageProperty(
                    ComponentDeclarationNames.Canonicalize(argument.Content),
                    argument.Content,
                    IsListenerName(argument.Content)
                        ? ComponentUsagePropertyKind.Listener
                        : ComponentUsagePropertyKind.Bound,
                    ClassifyExpression(directive.Expression),
                    Compose(filePath, blockContentStart, argument.Location)));
                return;
            }

            case "on":
            {
                if (directive.Argument is not SimpleExpressionNode { IsStatic: true } handler)
                {
                    suppliesUnknownArguments = true;
                    return;
                }

                properties.Add(new ComponentUsageProperty(
                    ComponentDeclarationNames.Canonicalize(handler.Content),
                    handler.Content,
                    ComponentUsagePropertyKind.Listener,
                    ComponentValueKind.Unknown,
                    Compose(filePath, blockContentStart, handler.Location)));
                return;
            }

            case "model":
            {
                // v-model on a component supplies its target argument plus the matching update
                // listener. The argument name is the directive's own argument, defaulting to
                // `modelValue`; the bound expression's type is a member reference, never a literal.
                var name = directive.Argument is SimpleExpressionNode { IsStatic: true } target
                    ? target.Content
                    : "modelValue";
                if (directive.Argument is not null &&
                    directive.Argument is not SimpleExpressionNode { IsStatic: true })
                {
                    suppliesUnknownArguments = true;
                    return;
                }

                properties.Add(new ComponentUsageProperty(
                    ComponentDeclarationNames.Canonicalize(name),
                    name,
                    ComponentUsagePropertyKind.Bound,
                    ComponentValueKind.Unknown,
                    Compose(filePath, blockContentStart, directive.Location)));
                return;
            }

            default:
                properties.Add(new ComponentUsageProperty(
                    directive.Name,
                    directive.RawName ?? directive.Name,
                    ComponentUsagePropertyKind.Directive,
                    ComponentValueKind.Unknown,
                    Compose(filePath, blockContentStart, directive.Location)));
                return;
        }
    }

    // The supplied value's type, decided only for a C# literal. The template compiler keeps expression
    // bodies opaque text, and the projection has no semantic model for them, so anything that is not a
    // literal stays Unknown and is never reported.
    private static ComponentValueKind ClassifyExpression(ExpressionNode? expression)
    {
        if (expression is not SimpleExpressionNode { IsStatic: false } simple)
        {
            return ComponentValueKind.Unknown;
        }

        var text = simple.Content.Trim();
        if (text.Length == 0)
        {
            return ComponentValueKind.Unknown;
        }

        if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"' &&
            text.IndexOf('"', 1, text.Length - 2) < 0)
        {
            return ComponentValueKind.Text;
        }

        if (string.Equals(text, "true", StringComparison.Ordinal) ||
            string.Equals(text, "false", StringComparison.Ordinal))
        {
            return ComponentValueKind.Value;
        }

        return IsNumericLiteral(text) ? ComponentValueKind.Value : ComponentValueKind.Unknown;
    }

    private static bool IsNumericLiteral(string text)
    {
        var index = text[0] == '-' || text[0] == '+' ? 1 : 0;
        if (index >= text.Length)
        {
            return false;
        }

        var digits = 0;
        var separators = 0;
        for (; index < text.Length; index++)
        {
            var character = text[index];
            if (character >= '0' && character <= '9')
            {
                digits++;
                continue;
            }

            if (character == '.' && separators == 0 && digits > 0)
            {
                separators++;
                continue;
            }

            return false;
        }

        return digits > 0;
    }

    // An `onX` spelling is a listener, declared or not: an undeclared one is a legitimate fallthrough
    // attribute [CMP-17], so it never participates in parameter validation.
    private static bool IsListenerName(string name)
        => name.Length > 2 && name[0] == 'o' && name[1] == 'n' && char.IsUpper(name[2]);

    // A usage-level diagnostic (a missing required parameter) squiggles the TAG NAME, not the element and
    // everything nested inside it. The element's own range spans its children, so the tag name is
    // reconstructed from the opening `<`: it always follows immediately, on the same line.
    private static LocationInfo TagNameLocation(
        ElementNode element,
        string filePath,
        Position blockContentStart)
    {
        var composed = Compose(filePath, blockContentStart, element.Location);
        var start = composed.StartOffset + 1;
        return new LocationInfo(
            filePath,
            start,
            start + element.Tag.Length,
            composed.StartLine,
            composed.StartCharacter + 1,
            composed.StartLine,
            composed.StartCharacter + 1 + element.Tag.Length);
    }

    private static LocationInfo Compose(
        string filePath,
        Position blockContentStart,
        SourceLocation location)
        => SingleFileComponentDiagnostics.ComposeLocation(filePath, location, blockContentStart);
}
