using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Classifies C# identifier tokens from their bound symbols, using the same symbol distinctions the
/// ordinary C# editor exposes without taking a dependency on Roslyn Workspaces or Features.
/// [V01.01.12.07.11]
/// </summary>
internal static class ScriptSemanticClassifier
{
    /// <summary>Classifies every bound identifier in a syntax tree in source order.</summary>
    internal static IReadOnlyList<ScriptSemanticClassification> Classify(
        SemanticModel semanticModel,
        SyntaxNode root,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(semanticModel);
        ArgumentNullException.ThrowIfNull(root);

        var classifications = new List<ScriptSemanticClassification>();
        foreach (var token in root.DescendantTokens())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!token.IsKind(SyntaxKind.IdentifierToken) ||
                TryGetSymbol(semanticModel, token, cancellationToken) is not { } symbol ||
                ToClassificationTypeName(symbol) is not { } classificationTypeName)
            {
                continue;
            }

            classifications.Add(new ScriptSemanticClassification(token.Span, classificationTypeName));
        }

        return classifications;
    }

    private static ISymbol? TryGetSymbol(
        SemanticModel semanticModel,
        SyntaxToken token,
        CancellationToken cancellationToken)
    {
        var node = token.Parent;
        if (node is null)
        {
            return null;
        }

        ISymbol? declared = node switch
        {
            BaseTypeDeclarationSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            DelegateDeclarationSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            MethodDeclarationSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            LocalFunctionStatementSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            PropertyDeclarationSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            EventDeclarationSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            VariableDeclaratorSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            ParameterSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            TypeParameterSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            EnumMemberDeclarationSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            LabeledStatementSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            CatchDeclarationSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            SingleVariableDesignationSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            ForEachStatementSyntax declaration when declaration.Identifier == token
                => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            _ => null,
        };
        if (declared is not null)
        {
            return declared;
        }

        if (node is IdentifierNameSyntax identifierName &&
            semanticModel.GetAliasInfo(identifierName, cancellationToken) is { } alias)
        {
            return alias.Target;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken);
        return symbolInfo.Symbol ??
            (symbolInfo.CandidateSymbols.Length > 0 ? symbolInfo.CandidateSymbols[0] : null);
    }

    private static string? ToClassificationTypeName(ISymbol symbol)
        => symbol switch
        {
            IAliasSymbol alias => ToClassificationTypeName(alias.Target),
            INamespaceSymbol => LanguageClassificationTypeNames.NamespaceName,
            INamedTypeSymbol { TypeKind: TypeKind.Class } => LanguageClassificationTypeNames.ClassName,
            INamedTypeSymbol { TypeKind: TypeKind.Delegate } => LanguageClassificationTypeNames.DelegateName,
            INamedTypeSymbol { TypeKind: TypeKind.Enum } => LanguageClassificationTypeNames.EnumName,
            INamedTypeSymbol { TypeKind: TypeKind.Interface } => LanguageClassificationTypeNames.InterfaceName,
            INamedTypeSymbol { TypeKind: TypeKind.Struct } => LanguageClassificationTypeNames.StructName,
            ITypeParameterSymbol => LanguageClassificationTypeNames.TypeParameterName,
            IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor }
                method => ToClassificationTypeName(method.ContainingType),
            IMethodSymbol => LanguageClassificationTypeNames.MethodName,
            IPropertySymbol => LanguageClassificationTypeNames.PropertyName,
            IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum }
                => LanguageClassificationTypeNames.EnumMemberName,
            IFieldSymbol { IsConst: true } => LanguageClassificationTypeNames.ConstantName,
            IFieldSymbol => LanguageClassificationTypeNames.FieldName,
            IEventSymbol => LanguageClassificationTypeNames.EventName,
            IParameterSymbol => LanguageClassificationTypeNames.ParameterName,
            ILocalSymbol { IsConst: true } => LanguageClassificationTypeNames.ConstantName,
            ILocalSymbol or IRangeVariableSymbol => LanguageClassificationTypeNames.LocalName,
            ILabelSymbol => LanguageClassificationTypeNames.LabelName,
            _ => null,
        };
}
