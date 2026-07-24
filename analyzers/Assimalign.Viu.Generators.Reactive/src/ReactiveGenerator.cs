using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Assimalign.Viu.Generators.Reactivity;

/// <summary>
/// The incremental source generator behind <c>[Reactive]</c>/<c>[ShallowReactive]</c> — the compiled
/// C# substitute for Vue 3.5's <c>reactive()</c>/<c>shallowReactive()</c>
/// (https://vuejs.org/api/reactivity-core.html#reactive). It fills in each declared <c>partial</c>
/// property with a per-property <c>Dependency</c>: the getter tracks, the setter triggers only
/// on an <c>EqualityComparer&lt;T&gt;</c> change, and the type gains an <c>IReactiveObject</c>
/// implementation for raw access, dependency lookup, and deep traversal. Interception is compiled in
/// because C# has no <c>Proxy</c> and WASM forbids reflection and runtime code generation.
/// <para>
/// The pipeline is fully incremental: the extracted model is a value-equatable record with no syntax
/// nodes or symbols, so an unrelated edit re-emits nothing.
/// </para>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ReactiveGenerator : IIncrementalGenerator
{
    private const string ReactiveAttributeName = "Assimalign.Viu.Reactivity.ReactiveAttribute";
    private const string ShallowReactiveAttributeName = "Assimalign.Viu.Reactivity.ShallowReactiveAttribute";

    /// <summary>Pipeline step tracking name for the deep model transform (used by incremental-cache tests).</summary>
    public const string DeepModelTrackingName = "ReactiveDeepModel";

    /// <summary>Pipeline step tracking name for the shallow model transform (used by incremental-cache tests).</summary>
    public const string ShallowModelTrackingName = "ReactiveShallowModel";

    private static readonly SymbolDisplayFormat FullyQualifiedNullable =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var deep = context.SyntaxProvider.ForAttributeWithMetadataName(
            ReactiveAttributeName,
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (syntaxContext, _) => BuildResult(syntaxContext, shallow: false))
            .WithTrackingName(DeepModelTrackingName);

        var shallow = context.SyntaxProvider.ForAttributeWithMetadataName(
            ShallowReactiveAttributeName,
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (syntaxContext, _) => BuildResult(syntaxContext, shallow: true))
            .WithTrackingName(ShallowModelTrackingName);

        context.RegisterSourceOutput(deep, static (production, result) => Execute(production, result));
        context.RegisterSourceOutput(shallow, static (production, result) => Execute(production, result));
    }

    private static void Execute(SourceProductionContext context, ReactiveGeneratorResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic());
        }
        if (result.Model is { } model)
        {
            context.AddSource(model.HintName, ReactiveSourceEmitter.Emit(model));
        }
    }

    private static ReactiveGeneratorResult BuildResult(GeneratorAttributeSyntaxContext context, bool shallow)
    {
        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var hasDeep = HasAttribute(symbol, ReactiveAttributeName);
        var hasShallow = HasAttribute(symbol, ShallowReactiveAttributeName);

        // Both attributes: the deep pass reports the conflict once; the shallow pass stays silent.
        if (hasDeep && hasShallow)
        {
            if (shallow)
            {
                return new ReactiveGeneratorResult(null, EquatableArray<DiagnosticInfo>.Empty);
            }
            return new ReactiveGeneratorResult(
                null,
                Single(new DiagnosticInfo(ReactiveDiagnostics.ConflictingAttributes, LocationInfo.From(context.TargetNode), symbol.Name)));
        }

        var diagnostics = new List<DiagnosticInfo>();

        if (symbol.IsStatic)
        {
            diagnostics.Add(new DiagnosticInfo(ReactiveDiagnostics.StaticType, LocationInfo.From(context.TargetNode), symbol.Name));
        }
        if (!IsPartial(symbol))
        {
            diagnostics.Add(new DiagnosticInfo(ReactiveDiagnostics.NotPartial, LocationInfo.From(context.TargetNode), symbol.Name));
        }
        if (symbol.IsStatic || !IsPartial(symbol))
        {
            // Cannot emit a partial implementation for a static or non-partial type.
            return new ReactiveGeneratorResult(null, ToArray(diagnostics));
        }

        var properties = CollectProperties(symbol, diagnostics);
        var isReadonly = ReadReadonlyFlag(context.Attributes);

        var model = new ReactiveClassModel(
            Namespace: symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString(),
            ContainingTypes: CollectContainingTypes(symbol),
            TypeName: symbol.Name,
            TypeParameterList: FormatTypeParameters(symbol.TypeParameters),
            AccessibilityKeyword: AccessibilityKeyword(symbol.DeclaredAccessibility),
            Shallow: shallow,
            Readonly: isReadonly,
            Properties: properties);

        return new ReactiveGeneratorResult(model, ToArray(diagnostics));
    }

    private static EquatableArray<ReactivePropertyModel> CollectProperties(INamedTypeSymbol symbol, List<DiagnosticInfo> diagnostics)
    {
        var models = new List<ReactivePropertyModel>();
        foreach (var member in symbol.GetMembers())
        {
            if (member is not IPropertySymbol property || !property.IsPartialDefinition || property.PartialImplementationPart is not null)
            {
                continue;
            }
            if (property.GetMethod is null || property.SetMethod is null || property.SetMethod.IsInitOnly)
            {
                diagnostics.Add(new DiagnosticInfo(
                    ReactiveDiagnostics.UnsupportedProperty,
                    LocationInfo.From(property.DeclaringSyntaxReferences[0].GetSyntax()),
                    property.Name));
                continue;
            }
            var setterModifier = property.SetMethod.DeclaredAccessibility != property.DeclaredAccessibility
                ? AccessibilityKeyword(property.SetMethod.DeclaredAccessibility)
                : string.Empty;
            models.Add(new ReactivePropertyModel(
                Name: property.Name,
                TypeFullName: property.Type.ToDisplayString(FullyQualifiedNullable),
                ValueFieldName: "__" + property.Name + "Value",
                DependencyFieldName: "__" + property.Name + "Dependency",
                AccessibilityKeyword: AccessibilityKeyword(property.DeclaredAccessibility),
                SetterModifier: setterModifier,
                IsValueType: property.Type.IsValueType));
        }
        return new EquatableArray<ReactivePropertyModel>(models.ToArray());
    }

    private static EquatableArray<ContainingTypeInfo> CollectContainingTypes(INamedTypeSymbol symbol)
    {
        var containers = new List<ContainingTypeInfo>();
        for (var container = symbol.ContainingType; container is not null; container = container.ContainingType)
        {
            containers.Add(new ContainingTypeInfo(
                TypeKeyword(container),
                container.Name,
                FormatTypeParameters(container.TypeParameters)));
        }
        containers.Reverse();
        return new EquatableArray<ContainingTypeInfo>(containers.ToArray());
    }

    private static bool HasAttribute(INamedTypeSymbol symbol, string metadataName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == metadataName)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsPartial(INamedTypeSymbol symbol)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is TypeDeclarationSyntax declaration && declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ReadReadonlyFlag(ImmutableArray<AttributeData> attributes)
    {
        if (attributes.Length == 0)
        {
            return false;
        }
        foreach (var argument in attributes[0].NamedArguments)
        {
            if (argument.Key == "Readonly" && argument.Value.Value is bool value)
            {
                return value;
            }
        }
        return false;
    }

    private static string TypeKeyword(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind == TypeKind.Struct)
        {
            return symbol.IsRecord ? "record struct" : "struct";
        }
        return symbol.IsRecord ? "record" : "class";
    }

    private static string FormatTypeParameters(ImmutableArray<ITypeParameterSymbol> typeParameters)
    {
        if (typeParameters.Length == 0)
        {
            return string.Empty;
        }
        var builder = new StringBuilder("<");
        for (var index = 0; index < typeParameters.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }
            builder.Append(typeParameters[index].Name);
        }
        builder.Append('>');
        return builder.ToString();
    }

    private static string AccessibilityKeyword(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public ",
        Accessibility.Internal => "internal ",
        Accessibility.Private => "private ",
        Accessibility.Protected => "protected ",
        Accessibility.ProtectedOrInternal => "protected internal ",
        Accessibility.ProtectedAndInternal => "private protected ",
        _ => string.Empty,
    };

    private static EquatableArray<DiagnosticInfo> Single(DiagnosticInfo diagnostic)
        => new(new[] { diagnostic });

    private static EquatableArray<DiagnosticInfo> ToArray(List<DiagnosticInfo> diagnostics)
        => diagnostics.Count == 0 ? EquatableArray<DiagnosticInfo>.Empty : new EquatableArray<DiagnosticInfo>(diagnostics.ToArray());
}
