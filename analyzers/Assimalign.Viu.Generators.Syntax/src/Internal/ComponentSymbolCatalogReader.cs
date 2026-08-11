using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.CodeAnalysis;

using Assimalign.Viu.Compiler.SingleFileComponent;

namespace Assimalign.Viu.Generators.Syntax;

/// <summary>
/// Reads the component declarations a compilation can see through Roslyn symbols — hand-authored
/// <c>IComponent</c> types in this compilation, and every component in a <b>referenced
/// assembly</b> ([SFC-USE-1]). The metadata half is what the [CMP-26] attribute form buys: a
/// <c>[Parameter]</c> survives into the assembly, so a package's component surface is readable by the
/// consumer's template compiler. A parameterless component still contributes its identity. A component
/// that declares parameters imperatively also contributes identity, but its arbitrary <c>Parameters</c>
/// collection marks the surface unknown and suppresses parameter checks ([SFC-USE-5]).
/// <para>
/// The scan is bounded on purpose. It returns immediately when the compilation does not reference
/// <c>Assimalign.Viu.Components</c> at all, and it walks only assemblies that reference that library, so
/// the framework's own assemblies are never enumerated.
/// </para>
/// </summary>
internal static class ComponentSymbolCatalogReader
{
    private const string ComponentsAssemblyName = "Assimalign.Viu.Components";
    private const string ComponentInterfaceName = "Assimalign.Viu.Components.IComponent";
    private const string ParameterAttributeName = "Assimalign.Viu.Components.ParameterAttribute";

    /// <summary>Reads every statically declared component surface visible to <paramref name="compilation"/>.</summary>
    /// <param name="compilation">The compilation being generated for.</param>
    /// <param name="cancellationToken">The token cancelling the scan.</param>
    /// <returns>The declarations, in a deterministic order.</returns>
    public static EquatableArray<ComponentDeclarationEntry> Read(
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var componentInterface = compilation.GetTypeByMetadataName(ComponentInterfaceName);
        var parameterAttribute = compilation.GetTypeByMetadataName(ParameterAttributeName);
        if (componentInterface is null || parameterAttribute is null)
        {
            return EquatableArray<ComponentDeclarationEntry>.Empty;
        }

        var entries = new List<ComponentDeclarationEntry>();
        var context = new ScanContext(componentInterface, parameterAttribute, entries, cancellationToken);
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

        entries.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        return entries.Count == 0
            ? EquatableArray<ComponentDeclarationEntry>.Empty
            : new EquatableArray<ComponentDeclarationEntry>(entries.ToArray());
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

        if (type.TypeKind != TypeKind.Class ||
            type.IsAbstract ||
            type.IsGenericType ||
            !ImplementsComponent(type, context.ComponentInterface))
        {
            return;
        }

        List<ComponentParameterDeclaration>? parameters = null;
        var declaresImperativeParameters = false;
        foreach (var member in type.GetMembers())
        {
            if (member is not IPropertySymbol property || property.IsStatic)
            {
                continue;
            }

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

        context.Entries.Add(new ComponentDeclarationEntry(
            type.Name,
            parameters is null
                ? EquatableArray<ComponentParameterDeclaration>.Empty
                : new EquatableArray<ComponentParameterDeclaration>(parameters.ToArray()))
        {
            IsParameterSurfaceKnown = !declaresImperativeParameters,
        });
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

            var name = ComponentDeclarationNames.Derive(property.Name);
            if (attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Value is string positional &&
                positional.Length > 0)
            {
                name = positional;
            }

            var isRequired = property.IsRequired;
            foreach (var named in attribute.NamedArguments)
            {
                if (string.Equals(named.Key, "Name", StringComparison.Ordinal) &&
                    named.Value.Value is string explicitName &&
                    explicitName.Length > 0)
                {
                    name = explicitName;
                }
                else if (string.Equals(named.Key, "IsRequired", StringComparison.Ordinal) &&
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

    // The symbol path knows exactly what the syntactic path can only guess, so it decides the kind from
    // the type itself rather than from a keyword table.
    private static ComponentValueKind Classify(ITypeSymbol type)
        => type switch
        {
            { SpecialType: SpecialType.System_String } => ComponentValueKind.Text,
            { SpecialType: SpecialType.System_Object } => ComponentValueKind.Any,
            { IsValueType: true } => ComponentValueKind.Value,
            { TypeKind: TypeKind.TypeParameter } => ComponentValueKind.Unknown,
            _ => ComponentValueKind.Reference,
        };

    private static bool ImplementsComponent(INamedTypeSymbol type, INamedTypeSymbol componentInterface)
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

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private readonly struct ScanContext(
        INamedTypeSymbol componentInterface,
        INamedTypeSymbol parameterAttribute,
        List<ComponentDeclarationEntry> entries,
        CancellationToken cancellationToken)
    {
        public INamedTypeSymbol ComponentInterface { get; } = componentInterface;

        public INamedTypeSymbol ParameterAttribute { get; } = parameterAttribute;

        public List<ComponentDeclarationEntry> Entries { get; } = entries;

        public CancellationToken CancellationToken { get; } = cancellationToken;
    }
}
