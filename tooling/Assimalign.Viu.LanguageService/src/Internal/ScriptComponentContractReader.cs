using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.CodeAnalysis;

using Assimalign.Viu.Compiler.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Reads template-facing component contracts from the same Roslyn compilation that backs projected
/// script features. The parameter rules are the compiler's [SFC-USE-5] symbol rules; event metadata
/// is retained for editor completion. [V01.01.12.07.12]
/// </summary>
internal static class ScriptComponentContractReader
{
    private const string ComponentsAssemblyName = "Assimalign.Viu.Components";
    private const string ComponentInterfaceName = "Assimalign.Viu.Components.IComponent";
    private const string ParameterAttributeName = "Assimalign.Viu.Components.ParameterAttribute";
    private const string EventAttributeName = "Assimalign.Viu.Components.EventAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly SymbolDisplayFormat EventDetailFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters |
            SymbolDisplayMemberOptions.IncludeType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType |
            SymbolDisplayParameterOptions.IncludeName |
            SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    internal static IReadOnlyList<TemplateComponentDeclaration> Read(
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var componentInterface = compilation.GetTypeByMetadataName(ComponentInterfaceName);
        var parameterAttribute = compilation.GetTypeByMetadataName(ParameterAttributeName);
        var eventAttribute = compilation.GetTypeByMetadataName(EventAttributeName);
        if (componentInterface is null || parameterAttribute is null)
        {
            return Array.Empty<TemplateComponentDeclaration>();
        }

        var declarations = new List<TemplateComponentDeclaration>();
        var context = new ScanContext(
            componentInterface,
            parameterAttribute,
            eventAttribute,
            declarations,
            cancellationToken);
        ScanAssembly(compilation.Assembly, context);
        foreach (var reference in compilation.References)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly &&
                ReferencesComponents(assembly))
            {
                ScanAssembly(assembly, context);
            }
        }

        declarations.Sort(static (left, right) => string.CompareOrdinal(
            left.CompilerDeclaration.Name,
            right.CompilerDeclaration.Name));
        return declarations;
    }

    private static bool ReferencesComponents(IAssemblySymbol assembly)
    {
        if (string.Equals(assembly.Name, ComponentsAssemblyName, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var module in assembly.Modules)
        {
            foreach (var identity in module.ReferencedAssemblies)
            {
                if (string.Equals(identity.Name, ComponentsAssemblyName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ScanAssembly(IAssemblySymbol assembly, in ScanContext context)
        => ScanNamespace(assembly.GlobalNamespace, context);

    private static void ScanNamespace(INamespaceSymbol containing, in ScanContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        foreach (var member in containing.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol nested:
                    ScanNamespace(nested, context);
                    break;
                case INamedTypeSymbol type:
                    ScanType(type, context);
                    break;
            }
        }
    }

    private static void ScanType(INamedTypeSymbol type, in ScanContext context)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            ScanType(nested, context);
        }

        // Source-generator projections are read from their projection model instead. That model is
        // the compiler's authoritative syntactic declaration surface even while an attribute is
        // incomplete or unresolved in the live Roslyn compilation.
        if (IsProjectedSingleFileComponent(type) ||
            type.TypeKind != TypeKind.Class ||
            type.IsAbstract ||
            type.IsGenericType ||
            !ImplementsComponent(type, context.ComponentInterface))
        {
            return;
        }

        List<ComponentParameterDeclaration>? parameters = null;
        List<TemplateComponentEvent>? events = null;
        var declaresImperativeParameters = false;
        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol { IsStatic: false } property)
            {
                if (property.SetMethod is not null &&
                    ReadParameter(property, context.ParameterAttribute) is { } declaration)
                {
                    (parameters ??= new List<ComponentParameterDeclaration>()).Add(declaration);
                }
                else if (string.Equals(property.Name, "Parameters", StringComparison.Ordinal))
                {
                    declaresImperativeParameters = true;
                }
            }
            else if (member is IMethodSymbol { IsStatic: false } method &&
                context.EventAttribute is not null &&
                ReadEvent(method, context.EventAttribute) is { } componentEvent)
            {
                (events ??= new List<TemplateComponentEvent>()).Add(componentEvent);
            }
        }

        parameters?.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        events?.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        context.Declarations.Add(
            new TemplateComponentDeclaration(
                new ComponentDeclarationEntry(
                    type.Name,
                    parameters is null
                        ? EquatableArray<ComponentParameterDeclaration>.Empty
                        : new EquatableArray<ComponentParameterDeclaration>(parameters.ToArray()))
                {
                    IsParameterSurfaceKnown = !declaresImperativeParameters,
                },
                events ?? (IReadOnlyList<TemplateComponentEvent>)Array.Empty<TemplateComponentEvent>()));
    }

    private static bool IsProjectedSingleFileComponent(INamedTypeSymbol type)
    {
        foreach (var location in type.Locations)
        {
            if (!location.IsInSource)
            {
                continue;
            }

            var filePath = location.SourceTree?.FilePath;
            if (filePath is not null &&
                (filePath.EndsWith(".viu", StringComparison.OrdinalIgnoreCase) ||
                    filePath.EndsWith(".vue", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static ComponentParameterDeclaration? ReadParameter(
        IPropertySymbol property,
        INamedTypeSymbol parameterAttribute)
    {
        foreach (var attribute in property.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, parameterAttribute))
            {
                continue;
            }

            var name = ReadDeclaredName(attribute, ComponentDeclarationNames.Derive(property.Name));
            var isRequired = property.IsRequired;
            foreach (var named in attribute.NamedArguments)
            {
                if (string.Equals(named.Key, "IsRequired", StringComparison.Ordinal) &&
                    named.Value.Value is bool required)
                {
                    isRequired |= required;
                }
            }

            return new ComponentParameterDeclaration(
                name,
                property.Name,
                property.Type.ToDisplayString(TypeFormat),
                Classify(property.Type),
                isRequired,
                property.IsRequired);
        }

        return null;
    }

    private static TemplateComponentEvent? ReadEvent(
        IMethodSymbol method,
        INamedTypeSymbol eventAttribute)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, eventAttribute))
            {
                return new TemplateComponentEvent(
                    ReadDeclaredName(attribute, ComponentDeclarationNames.Derive(method.Name)),
                    method.ToDisplayString(EventDetailFormat));
            }
        }

        return null;
    }

    private static string ReadDeclaredName(AttributeData attribute, string derivedName)
    {
        var name = derivedName;
        if (attribute.ConstructorArguments.Length == 1 &&
            attribute.ConstructorArguments[0].Value is string positional &&
            positional.Length > 0)
        {
            name = positional;
        }

        foreach (var named in attribute.NamedArguments)
        {
            if (string.Equals(named.Key, "Name", StringComparison.Ordinal) &&
                named.Value.Value is string explicitName &&
                explicitName.Length > 0)
            {
                name = explicitName;
            }
        }

        return name;
    }

    private static ComponentValueKind Classify(ITypeSymbol type)
        => type switch
        {
            { SpecialType: SpecialType.System_String } => ComponentValueKind.Text,
            { SpecialType: SpecialType.System_Object } => ComponentValueKind.Any,
            { IsValueType: true } => ComponentValueKind.Value,
            { TypeKind: TypeKind.TypeParameter } => ComponentValueKind.Unknown,
            _ => ComponentValueKind.Reference,
        };

    private static bool ImplementsComponent(
        INamedTypeSymbol type,
        INamedTypeSymbol componentInterface)
    {
        foreach (var implemented in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(implemented, componentInterface))
            {
                return true;
            }
        }

        return false;
    }

    private readonly struct ScanContext(
        INamedTypeSymbol componentInterface,
        INamedTypeSymbol parameterAttribute,
        INamedTypeSymbol? eventAttribute,
        List<TemplateComponentDeclaration> declarations,
        CancellationToken cancellationToken)
    {
        internal INamedTypeSymbol ComponentInterface { get; } = componentInterface;

        internal INamedTypeSymbol ParameterAttribute { get; } = parameterAttribute;

        internal INamedTypeSymbol? EventAttribute { get; } = eventAttribute;

        internal List<TemplateComponentDeclaration> Declarations { get; } = declarations;

        internal CancellationToken CancellationToken { get; } = cancellationToken;
    }
}
