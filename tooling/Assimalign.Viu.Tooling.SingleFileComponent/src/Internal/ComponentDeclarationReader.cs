using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Assimalign.Viu.Syntax;

namespace Assimalign.Viu.Tooling.SingleFileComponent;

/// <summary>
/// Reads the attribute-declared component surface — <c>[Parameter]</c> properties and <c>[Event]</c>
/// methods — out of the synthetic probe parse of an <c>@script</c> member region ([CMP-26], [CMP-30]).
/// Recognition is purely syntactic, like every other <see cref="ScriptBlockAnalyzer"/> rule: the build
/// host has no semantic model for the class it is itself generating, and reflection is forbidden, so an
/// attribute is matched by its simple name (<c>Parameter</c> or <c>ParameterAttribute</c>) and its
/// arguments must be constant literals. Anything the reader cannot decide is reported as a located
/// diagnostic rather than guessed, because a guessed declaration would ship a wrong runtime surface.
/// <para>
/// The reader also enforces the coexistence rule ([CMP-31]): a component declares its parameters either
/// imperatively — a <c>Parameters</c>/<c>Events</c> member the class implements itself — or by
/// attribute, never both for the same kind. Mixing them would leave the generated explicit interface
/// implementation silently shadowing the authored collection, and it would make the component's input
/// surface unreadable at build time, which is the whole point of the attribute form.
/// </para>
/// </summary>
internal static class ComponentDeclarationReader
{
    private const string ParametersMemberName = "Parameters";
    private const string EventsMemberName = "Events";

    /// <summary>
    /// Reads the attributed declarations from <paramref name="probe"/>, appending located diagnostics
    /// for unsupported, duplicate, or conflicting declarations to <paramref name="diagnostics"/>.
    /// </summary>
    /// <param name="probe">The synthetic partial-class probe wrapping the member region.</param>
    /// <param name="filePath">The originating single-file-component path.</param>
    /// <param name="memberRegionStart">The file position where the member region begins.</param>
    /// <param name="probePrefixLength">The synthetic probe prefix length, to un-shift offsets.</param>
    /// <param name="probeLineOffset">The synthetic probe's leading line count, to un-shift lines.</param>
    /// <param name="diagnostics">The diagnostic accumulator.</param>
    /// <returns>The declared surface, or <see cref="ScriptDeclarations.None"/> when none is declared.</returns>
    public static ScriptDeclarations Read(
        ClassDeclarationSyntax probe,
        string filePath,
        Position memberRegionStart,
        int probePrefixLength,
        int probeLineOffset,
        List<DiagnosticInfo> diagnostics)
    {
        List<ComponentParameterDeclaration>? parameters = null;
        List<ComponentEventDeclaration>? events = null;
        var parameterNames = new HashSet<string>(StringComparer.Ordinal);
        var eventNames = new HashSet<string>(StringComparer.Ordinal);
        var declaresRequiredMember = false;
        var declaresConstructor = false;
        SyntaxToken? explicitParametersMember = null;
        SyntaxToken? explicitEventsMember = null;
        SyntaxToken? firstParameterAttribute = null;
        SyntaxToken? firstEventAttribute = null;

        void Report(
            SingleFileComponentDiagnosticDescriptor descriptor,
            string message,
            Location location)
            => diagnostics.Add(SingleFileComponentDiagnostics.CreateScriptRule(
                descriptor,
                message,
                filePath,
                location,
                memberRegionStart,
                probePrefixLength,
                probeLineOffset));

        foreach (var member in probe.Members)
        {
            switch (member)
            {
                case ConstructorDeclarationSyntax:
                    declaresConstructor = true;
                    break;

                case FieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables)
                    {
                        RecordExplicitMember(
                            variable.Identifier,
                            ref explicitParametersMember,
                            ref explicitEventsMember);
                    }

                    break;

                case PropertyDeclarationSyntax property:
                {
                    RecordExplicitMember(
                        property.Identifier,
                        ref explicitParametersMember,
                        ref explicitEventsMember);

                    if (FindAttribute(property.AttributeLists, "Parameter") is not { } attribute)
                    {
                        break;
                    }

                    firstParameterAttribute ??= property.Identifier;
                    if (!TryReadParameter(property, attribute, Report, out var declaration))
                    {
                        break;
                    }

                    if (!parameterNames.Add(declaration.Name))
                    {
                        Report(
                            SingleFileComponentDiagnostics.DuplicateComponentDeclaration,
                            $"Component parameter '{declaration.Name}' is declared more than once. "
                            + "Rename the property or set an explicit [Parameter(Name = \"...\")].",
                            property.Identifier.GetLocation());
                        break;
                    }

                    declaresRequiredMember |= declaration.IsRequiredMember;
                    (parameters ??= new List<ComponentParameterDeclaration>()).Add(declaration);
                    break;
                }

                case MethodDeclarationSyntax method:
                {
                    if (FindAttribute(method.AttributeLists, "Event") is not { } attribute)
                    {
                        break;
                    }

                    firstEventAttribute ??= method.Identifier;
                    if (!TryReadEvent(method, attribute, Report, out var declaration))
                    {
                        break;
                    }

                    if (!eventNames.Add(declaration.Name))
                    {
                        Report(
                            SingleFileComponentDiagnostics.DuplicateComponentDeclaration,
                            $"Component event '{declaration.Name}' is declared more than once. "
                            + "Rename the method or set an explicit [Event(Name = \"...\")].",
                            method.Identifier.GetLocation());
                        break;
                    }

                    (events ??= new List<ComponentEventDeclaration>()).Add(declaration);
                    break;
                }
            }
        }

        // [CMP-31] The coexistence rule. Reported on the attributed member, because the imperative
        // collection is the form that keeps working unchanged and the attribute is the new declaration.
        if (firstParameterAttribute is { } parameterAttribute && explicitParametersMember is not null)
        {
            Report(
                SingleFileComponentDiagnostics.ConflictingComponentDeclaration,
                "This component declares parameters both with [Parameter] and with its own "
                + "'Parameters' member. Choose one form: the generated declaration would otherwise "
                + "shadow the authored collection.",
                parameterAttribute.GetLocation());
            parameters = null;
        }

        if (firstEventAttribute is { } eventAttribute && explicitEventsMember is not null)
        {
            Report(
                SingleFileComponentDiagnostics.ConflictingComponentDeclaration,
                "This component declares events both with [Event] and with its own 'Events' member. "
                + "Choose one form: the generated declaration would otherwise shadow the authored "
                + "collection.",
                eventAttribute.GetLocation());
            events = null;
        }

        if (parameters is null && events is null)
        {
            return declaresConstructor
                ? ScriptDeclarations.None with { DeclaresConstructor = true }
                : ScriptDeclarations.None;
        }

        return new ScriptDeclarations(
            parameters is null
                ? EquatableArray<ComponentParameterDeclaration>.Empty
                : new EquatableArray<ComponentParameterDeclaration>(parameters.ToArray()),
            events is null
                ? EquatableArray<ComponentEventDeclaration>.Empty
                : new EquatableArray<ComponentEventDeclaration>(events.ToArray()),
            declaresRequiredMember && parameters is not null,
            declaresConstructor);
    }

    private static void RecordExplicitMember(
        SyntaxToken identifier,
        ref SyntaxToken? explicitParametersMember,
        ref SyntaxToken? explicitEventsMember)
    {
        if (string.Equals(identifier.Text, ParametersMemberName, StringComparison.Ordinal))
        {
            explicitParametersMember ??= identifier;
        }
        else if (string.Equals(identifier.Text, EventsMemberName, StringComparison.Ordinal))
        {
            explicitEventsMember ??= identifier;
        }
    }

    private static bool TryReadParameter(
        PropertyDeclarationSyntax property,
        AttributeSyntax attribute,
        Action<SingleFileComponentDiagnosticDescriptor, string, Location> report,
        out ComponentParameterDeclaration declaration)
    {
        declaration = default;
        if (HasModifier(property.Modifiers, SyntaxKind.StaticKeyword))
        {
            report(
                SingleFileComponentDiagnostics.UnsupportedComponentDeclaration,
                $"[Parameter] property '{property.Identifier.Text}' must be an instance property: "
                + "an argument is supplied per mounted component.",
                property.Identifier.GetLocation());
            return false;
        }

        if (!HasSetAccessor(property))
        {
            report(
                SingleFileComponentDiagnostics.UnsupportedComponentDeclaration,
                $"[Parameter] property '{property.Identifier.Text}' needs a 'set' accessor: the "
                + "generated scaffold assigns the supplied argument to it before every render.",
                property.Identifier.GetLocation());
            return false;
        }

        if (!TryReadName(attribute, property.Identifier, report, out var name) ||
            !TryReadIsRequired(attribute, property.Identifier, report, out var isRequired))
        {
            return false;
        }

        var isRequiredMember = HasModifier(property.Modifiers, SyntaxKind.RequiredKeyword);
        var typeText = property.Type.ToString();
        declaration = new ComponentParameterDeclaration(
            name,
            property.Identifier.Text,
            typeText,
            ComponentDeclarationNames.ClassifyTypeText(typeText),
            isRequired || isRequiredMember,
            isRequiredMember);
        return true;
    }

    private static bool TryReadEvent(
        MethodDeclarationSyntax method,
        AttributeSyntax attribute,
        Action<SingleFileComponentDiagnosticDescriptor, string, Location> report,
        out ComponentEventDeclaration declaration)
    {
        declaration = default;
        var isPartialVoidDeclaration =
            HasModifier(method.Modifiers, SyntaxKind.PartialKeyword) &&
            !HasModifier(method.Modifiers, SyntaxKind.StaticKeyword) &&
            method.Body is null &&
            method.ExpressionBody is null &&
            method.TypeParameterList is null &&
            method.ReturnType is PredefinedTypeSyntax returnType &&
            returnType.Keyword.IsKind(SyntaxKind.VoidKeyword);
        if (!isPartialVoidDeclaration)
        {
            report(
                SingleFileComponentDiagnostics.UnsupportedComponentDeclaration,
                $"[Event] method '{method.Identifier.Text}' must be declared as a non-generic, "
                + "instance 'partial void' method with no body; the generated scaffold implements it "
                + "as the typed emit of the declared event.",
                method.Identifier.GetLocation());
            return false;
        }

        if (!TryReadName(attribute, method.Identifier, report, out var name))
        {
            return false;
        }

        var parameterList = new StringBuilder("(");
        var argumentList = new StringBuilder();
        var index = 0;
        foreach (var parameter in method.ParameterList.Parameters)
        {
            if (parameter.Modifiers.Count > 0 || parameter.Type is null)
            {
                report(
                    SingleFileComponentDiagnostics.UnsupportedComponentDeclaration,
                    $"[Event] method '{method.Identifier.Text}' parameter "
                    + $"'{parameter.Identifier.Text}' must be a plain by-value parameter: an emitted "
                    + "argument is copied into the ordered payload list.",
                    parameter.Identifier.GetLocation());
                return false;
            }

            if (index > 0)
            {
                parameterList.Append(", ");
                argumentList.Append(", ");
            }

            // The default value is deliberately dropped: C# rejects a repeated default on a partial
            // method's implementing declaration, and the defining declaration the author wrote keeps it.
            parameterList.Append(parameter.Type.ToString()).Append(' ').Append(parameter.Identifier.Text);
            argumentList.Append(parameter.Identifier.Text);
            index++;
        }

        parameterList.Append(')');
        declaration = new ComponentEventDeclaration(
            name,
            method.Identifier.Text,
            method.Modifiers.ToString(),
            parameterList.ToString(),
            argumentList.ToString(),
            index);
        return true;
    }

    private static bool TryReadName(
        AttributeSyntax attribute,
        SyntaxToken memberIdentifier,
        Action<SingleFileComponentDiagnosticDescriptor, string, Location> report,
        out string name)
    {
        name = ComponentDeclarationNames.Derive(memberIdentifier.Text);
        if (attribute.ArgumentList is not { } arguments)
        {
            return true;
        }

        foreach (var argument in arguments.Arguments)
        {
            var isName = argument.NameEquals is null
                ? argument.NameColon is null || argument.NameColon.Name.Identifier.Text == "name"
                : argument.NameEquals.Name.Identifier.Text == "Name";
            if (!isName)
            {
                continue;
            }

            if (argument.Expression is not LiteralExpressionSyntax literal ||
                !literal.Token.IsKind(SyntaxKind.StringLiteralToken) ||
                literal.Token.ValueText.Length == 0)
            {
                report(
                    SingleFileComponentDiagnostics.UnsupportedComponentDeclaration,
                    $"The name declared for '{memberIdentifier.Text}' must be a non-empty constant "
                    + "string literal: the generated declaration is emitted at build time.",
                    argument.GetLocation());
                return false;
            }

            name = literal.Token.ValueText;
            return true;
        }

        return true;
    }

    private static bool TryReadIsRequired(
        AttributeSyntax attribute,
        SyntaxToken memberIdentifier,
        Action<SingleFileComponentDiagnosticDescriptor, string, Location> report,
        out bool isRequired)
    {
        isRequired = false;
        if (attribute.ArgumentList is not { } arguments)
        {
            return true;
        }

        foreach (var argument in arguments.Arguments)
        {
            if (argument.NameEquals?.Name.Identifier.Text != "IsRequired")
            {
                continue;
            }

            if (argument.Expression.IsKind(SyntaxKind.TrueLiteralExpression))
            {
                isRequired = true;
                return true;
            }

            if (argument.Expression.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                return true;
            }

            report(
                SingleFileComponentDiagnostics.UnsupportedComponentDeclaration,
                $"The IsRequired argument declared for '{memberIdentifier.Text}' must be the literal "
                + "'true' or 'false': the generated declaration is emitted at build time.",
                argument.GetLocation());
            return false;
        }

        return true;
    }

    // The attribute is matched on its right-most simple name, with and without the Attribute suffix, so
    // `[Parameter]`, `[ParameterAttribute]`, and `[Assimalign.Viu.Components.Parameter]` all recognize.
    private static AttributeSyntax? FindAttribute(
        Microsoft.CodeAnalysis.SyntaxList<AttributeListSyntax> attributeLists,
        string simpleName)
    {
        foreach (var attributeList in attributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = SimpleName(attribute.Name);
                if (string.Equals(name, simpleName, StringComparison.Ordinal) ||
                    string.Equals(name, simpleName + "Attribute", StringComparison.Ordinal))
                {
                    return attribute;
                }
            }
        }

        return null;
    }

    private static string? SimpleName(NameSyntax name)
        => name switch
        {
            GenericNameSyntax generic => generic.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            QualifiedNameSyntax qualified => SimpleName(qualified.Right),
            AliasQualifiedNameSyntax alias => SimpleName(alias.Name),
            _ => null,
        };

    private static bool HasSetAccessor(PropertyDeclarationSyntax property)
    {
        if (property.AccessorList is null)
        {
            return false;
        }

        foreach (var accessor in property.AccessorList.Accessors)
        {
            if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasModifier(SyntaxTokenList modifiers, SyntaxKind kind)
    {
        foreach (var modifier in modifiers)
        {
            if (modifier.IsKind(kind))
            {
                return true;
            }
        }

        return false;
    }
}
